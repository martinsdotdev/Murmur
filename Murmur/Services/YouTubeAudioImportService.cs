using System;
using System.Linq;
using System.Threading.Tasks;
using Murmur.Models;
using Windows.Media.Core;
using Windows.Media.Streaming.Adaptive;
using YoutubeExplode;
using YoutubeExplode.Exceptions;
using YoutubeExplode.Videos;
using YoutubeExplode.Videos.Streams;

namespace Murmur.Services;

/// <summary>User-added sounds backed by YouTube URLs. Resolves a playable stream via
/// YoutubeExplode and registers the result on the mixer (no local download).</summary>
public sealed class YouTubeAudioImportService
{
    private readonly IMixerService _mixer;
    private readonly SoundCatalog _catalog;
    private readonly AppState _state;
    private readonly YoutubeClient _youtube = new();

    public YouTubeAudioImportService(IMixerService mixer, SoundCatalog catalog, AppState state)
    {
        _mixer = mixer;
        _catalog = catalog;
        _state = state;
    }

    private async Task<MediaSource> ResolveMediaSourceAsync(string watchUrl)
    {
        var video = await _youtube.Videos.GetAsync(watchUrl);

        // Video.Duration is a HINT, not a guarantee. Some active livestreams report elapsed
        // runtime as non-null Duration; some unusual finite videos have null Duration. Pick
        // the most likely path and fall back on YoutubeExplodeException.
        if (video.Duration is null)
        {
            try { return await CreateLiveMediaSourceAsync(video); }
            catch (YoutubeExplodeException ex)
            {
                DiagnosticLog.Log($"Live resolve failed for '{video.Id}' (Duration null), falling back to manifest. {ex.GetType().Name}: {ex.Message}");
                return await CreateFiniteMediaSourceAsync(video);
            }
        }

        try { return await CreateFiniteMediaSourceAsync(video); }
        catch (YoutubeExplodeException ex)
        {
            DiagnosticLog.Log($"Manifest resolve failed for '{video.Id}' (Duration {video.Duration}), falling back to live. {ex.GetType().Name}: {ex.Message}");
            return await CreateLiveMediaSourceAsync(video);
        }
    }

    private async Task<MediaSource> CreateLiveMediaSourceAsync(Video video)
    {
        var hlsUrl = await _youtube.Videos.Streams.GetHttpLiveStreamUrlAsync(video.Id);

        // HLS livestreams need AdaptiveMediaSource, plain MediaSource.CreateFromUri can't
        // parse the m3u8 manifest into segments AudioGraph can decode (UnknownFailure).
        var amsResult = await AdaptiveMediaSource.CreateFromUriAsync(new Uri(hlsUrl));
        if (amsResult.Status != AdaptiveMediaSourceCreationStatus.Success)
        {
            throw new InvalidOperationException(
                $"Couldn't open livestream manifest: {amsResult.Status} ({amsResult.ExtendedError?.Message})");
        }
        // The MediaSource takes ownership: disposing it disposes the AdaptiveMediaSource too.
        return MediaSource.CreateFromAdaptiveMediaSource(amsResult.MediaSource);
    }

    private async Task<MediaSource> CreateFiniteMediaSourceAsync(Video video)
    {
        var manifest = await _youtube.Videos.Streams.GetManifestAsync(video.Id);
        var audio = manifest.GetAudioOnlyStreams().GetWithHighestBitrate()
            ?? throw new InvalidOperationException("No audio-only stream is available for this video.");
        return MediaSource.CreateFromUri(new Uri(audio.Url));
    }

    public async Task<Sound> ImportAsync(string watchUrl)
    {
        if (!IsAcceptableYouTubeUrl(watchUrl, out var canonical))
        {
            throw new ArgumentException("Not a YouTube URL.", nameof(watchUrl));
        }

        var video = await _youtube.Videos.GetAsync(canonical);
        var title = video.Title;

        var id = _catalog.MakeUniqueId(title, "youtube");
        var sound = new Sound(
            id,
            title,
            "youtube",
            new Uri(canonical, UriKind.Absolute),
            SoundKind.Streaming);

        await _mixer.RegisterStreamingSoundAsync(sound, () => ResolveMediaSourceAsync(canonical));

        _catalog.Custom.Add(sound);
        _state.YouTubeAudios[id] = new YouTubeAudioRecord(canonical, title);
        _state.RequestSave();
        DiagnosticLog.Log($"Imported YouTube '{title}' as '{id}' ← {canonical}");
        return sound;
    }

    /// <summary>Validates a URL is HTTPS and points at a YouTube host. On success,
    /// <paramref name="canonical"/> is the trimmed, normalized URL.</summary>
    public static bool IsAcceptableYouTubeUrl(string watchUrl, out string canonical)
    {
        canonical = string.Empty;
        if (string.IsNullOrWhiteSpace(watchUrl)) return false;
        if (!Uri.TryCreate(watchUrl.Trim(), UriKind.Absolute, out var parsed)) return false;
        if (parsed.Scheme != Uri.UriSchemeHttps) return false;
        if (parsed.Host is not "www.youtube.com" and not "youtube.com" and not "youtu.be" and not "m.youtube.com")
            return false;
        canonical = parsed.ToString();
        return true;
    }

    public async Task RestoreSavedAsync()
    {
        var entries = _state.YouTubeAudios.ToArray();
        foreach (var (id, record) in entries)
        {
            try
            {
                var sound = new Sound(
                    id,
                    record.DisplayName,
                    "youtube",
                    new Uri(record.WatchUrl, UriKind.Absolute),
                    SoundKind.Streaming);

                // Defer resolve to first attach: keeps app launch fast and avoids burning
                // network on sounds the user might never play. Network failures here are
                // also non-permanent, the entry stays in state for next launch's retry.
                await _mixer.RegisterStreamingSoundAsync(sound,
                    () => ResolveMediaSourceAsync(record.WatchUrl));

                _catalog.Custom.Add(sound);
            }
            catch (Exception ex)
            {
                DiagnosticLog.Log($"YouTube restore failed for '{id}' ({record.WatchUrl}): {ex.Message}");
            }
        }
    }

    public void Delete(Sound sound)
    {
        if (sound.Kind != SoundKind.Streaming) return;

        _mixer.UnregisterSound(sound.Id);
        _catalog.Custom.Remove(sound);
        _state.RemoveSoundFromAllPresets(sound.Id);

        _state.YouTubeAudios.Remove(sound.Id);
        _state.RequestSave();
        DiagnosticLog.Log($"Deleted YouTube sound '{sound.Id}'.");
    }
}
