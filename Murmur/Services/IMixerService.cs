using System;
using System.Threading.Tasks;
using Murmur.Models;
using Windows.Media.Core;

namespace Murmur.Services;

/// <summary>Multi-stream looping mixer abstraction. The "silent-stream-paused" idiom
/// (volume == 0 → no audio-graph node, zero CPU) is implementation-required.</summary>
public interface IMixerService : IAsyncDisposable
{
    bool IsPlaying { get; }

    double MasterVolume { get; set; }

    event EventHandler? PlayStateChanged;

    /// <summary>Raised when a streaming sound's attach fails (resolve error, network drop,
    /// MediaSource failure). Payload is a user-facing message.</summary>
    event EventHandler<string>? StreamAttachFailed;

    Task InitializeAsync();

    Task RegisterSoundAsync(Sound sound);

    /// <summary><paramref name="resolveMediaSource"/> is invoked on every 0→>0 attach so
    /// livestream URLs stay fresh. Resolver returns a fully-built MediaSource because HLS
    /// needs AdaptiveMediaSource while finite videos use CreateFromUri — that format choice
    /// belongs in the import service, not the mixer.</summary>
    Task RegisterStreamingSoundAsync(Sound sound, Func<Task<MediaSource>> resolveMediaSource);

    void UnregisterSound(string soundId);

    void SetSoundVolume(string soundId, double volume);

    double GetSoundVolume(string soundId);

    void Play();

    void Pause();
}
