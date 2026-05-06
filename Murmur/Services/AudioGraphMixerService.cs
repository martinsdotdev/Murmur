using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Murmur.Models;
using NVorbis;
using Windows.Media;
using Windows.Media.Audio;
using Windows.Media.Core;
using Windows.Media.MediaProperties;
using Windows.Media.Render;
using WinRT;

namespace Murmur.Services;

/// <summary>Multi-stream looping mixer over a single always-running AudioGraph. Per-sound input
/// nodes are attached when volume &gt; 0 and disposed when it returns to 0 (silent-stream-paused).
/// Master Play/Pause is a gain mute on the output node, the graph itself stays running because
/// input nodes attached before the graph's first Start() never get pumped.</summary>
public sealed class AudioGraphMixerService : IMixerService
{
    private const uint GraphSampleRate = 44100;
    private const uint GraphChannels = 2;

    private AudioGraph? _graph;
    private AudioDeviceOutputNode? _output;
    private Task? _initTask;

    private readonly ConcurrentDictionary<string, SoundEntry> _sounds = new();
    private readonly ConcurrentDictionary<string, StreamingSoundEntry> _streamingSounds = new();
    private double _masterVolume = 1.0;

    public bool IsPlaying { get; private set; }

    public double MasterVolume
    {
        get => _masterVolume;
        set
        {
            _masterVolume = Math.Clamp(value, 0.0, 1.0);
            if (_output is not null && IsPlaying) _output.OutgoingGain = _masterVolume;
        }
    }

    public event EventHandler? PlayStateChanged;
    public event EventHandler<string>? StreamAttachFailed;

    public Task InitializeAsync() => _initTask ??= InitializeAsyncCore();

    private async Task InitializeAsyncCore()
    {
        DiagnosticLog.Log("InitializeAsync start");

        var graphProps = AudioEncodingProperties.CreatePcm(GraphSampleRate, GraphChannels, 32);
        graphProps.Subtype = MediaEncodingSubtypes.Float;

        var settings = new AudioGraphSettings(AudioRenderCategory.Media)
        {
            EncodingProperties = graphProps,
        };

        var graphResult = await AudioGraph.CreateAsync(settings);
        if (graphResult.Status != AudioGraphCreationStatus.Success)
        {
            throw new InvalidOperationException(
                $"AudioGraph creation failed: {graphResult.Status} ({graphResult.ExtendedError?.Message})");
        }
        _graph = graphResult.Graph;

        var deviceResult = await _graph.CreateDeviceOutputNodeAsync();
        if (deviceResult.Status != AudioDeviceNodeCreationStatus.Success)
        {
            throw new InvalidOperationException(
                $"Device output node creation failed: {deviceResult.Status} ({deviceResult.ExtendedError?.Message})");
        }
        _output = deviceResult.DeviceOutputNode;
        _output.OutgoingGain = 0;

        _graph.UnrecoverableErrorOccurred += (_, args) => DiagnosticLog.Log($"!! Graph error: {args.Error}");
        _graph.Start();

        DiagnosticLog.Log("InitializeAsync complete (graph started, output muted)");
    }

    public async ValueTask DisposeAsync()
    {
        Pause();
        foreach (var entry in _sounds.Values) DetachInputNode(entry);
        _sounds.Clear();
        foreach (var entry in _streamingSounds.Values) DetachStreamingInputNode(entry);
        _streamingSounds.Clear();
        _output?.Dispose();
        _output = null;
        _graph?.Dispose();
        _graph = null;
        await Task.CompletedTask;
    }

    public async Task RegisterSoundAsync(Sound sound)
    {
        if (_graph is null) throw new InvalidOperationException("Mixer not initialized.");
        var entry = await Task.Run(() => DecodeOgg(sound));
        if (!_sounds.TryAdd(sound.Id, entry))
            throw new InvalidOperationException($"Sound '{sound.Id}' is already registered.");
    }

    public Task RegisterStreamingSoundAsync(Sound sound, Func<Task<MediaSource>> resolveMediaSource)
    {
        if (_graph is null) throw new InvalidOperationException("Mixer not initialized.");
        var entry = new StreamingSoundEntry
        {
            Sound = sound,
            ResolveMediaSource = resolveMediaSource,
        };
        if (!_streamingSounds.TryAdd(sound.Id, entry))
            throw new InvalidOperationException($"Streaming sound '{sound.Id}' is already registered.");
        return Task.CompletedTask;
    }

    public void UnregisterSound(string soundId)
    {
        if (_sounds.TryRemove(soundId, out var entry))
        {
            DetachInputNode(entry);
            entry.Pcm = Array.Empty<float>();
            return;
        }
        if (_streamingSounds.TryRemove(soundId, out var sEntry))
        {
            DetachStreamingInputNode(sEntry);
        }
    }

    public void SetSoundVolume(string soundId, double volume)
    {
        volume = Math.Clamp(volume, 0.0, 1.0);

        if (_sounds.TryGetValue(soundId, out var entry))
        {
            entry.Volume = volume;
            bool shouldBeActive = ShouldBePlaying(volume);

            if (shouldBeActive && entry.InputNode is null) AttachInputNode(entry);
            else if (!shouldBeActive && entry.InputNode is not null) DetachInputNode(entry);
            else if (entry.InputNode is not null) entry.InputNode.OutgoingGain = volume;
            return;
        }

        if (_streamingSounds.TryGetValue(soundId, out var sEntry))
        {
            sEntry.Volume = volume;
            bool shouldBeActive = ShouldBePlaying(volume);

            if (shouldBeActive && sEntry.InputNode is null) _ = AttachStreamingInputNodeAsync(sEntry);
            else if (!shouldBeActive && sEntry.InputNode is not null) DetachStreamingInputNode(sEntry);
            else if (sEntry.InputNode is not null) sEntry.InputNode.OutgoingGain = volume;
            return;
        }

        throw new KeyNotFoundException($"Sound '{soundId}' is not registered.");
    }

    public double GetSoundVolume(string soundId)
    {
        if (_sounds.TryGetValue(soundId, out var e)) return e.Volume;
        if (_streamingSounds.TryGetValue(soundId, out var sE)) return sE.Volume;
        return 0.0;
    }

    /// <summary>Strict-zero gate (not epsilon, not hysteresis): UI sliders snap to 0 cleanly
    /// and we'd rather pay extra allocation than risk a residual click after the user mutes.</summary>
    private static bool ShouldBePlaying(double volume) => volume > 0;

    public void Play()
    {
        if (_output is null) throw new InvalidOperationException("Mixer not initialized.");
        IsPlaying = true;
        _output.OutgoingGain = _masterVolume;
        PlayStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Pause()
    {
        if (_output is null) return;
        IsPlaying = false;
        _output.OutgoingGain = 0;
        PlayStateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void AttachInputNode(SoundEntry entry)
    {
        if (_graph is null || _output is null)
            throw new InvalidOperationException("Mixer not initialized.");

        var props = AudioEncodingProperties.CreatePcm((uint)entry.SampleRate, (uint)entry.Channels, 32);
        props.Subtype = MediaEncodingSubtypes.Float;

        var node = _graph.CreateFrameInputNode(props);
        node.OutgoingGain = entry.Volume;
        node.AddOutgoingConnection(_output);

        entry.Position = 0;
        // IsActive=true must be set before subscribing QuantumStarted, otherwise the first
        // callback can observe a stale "false" from a prior detach and skip itself.
        entry.IsActive = true;
        node.QuantumStarted += (sender, args) => OnQuantumStarted(sender, args, entry);

        entry.InputNode = node;
    }

    private static void DetachInputNode(SoundEntry entry)
    {
        if (entry.InputNode is null) return;
        // IsActive=false BEFORE Dispose so OnQuantumStarted bails at its top-of-quantum check;
        // the residual race past that check is closed by the ObjectDisposedException catch
        // around AddFrame.
        entry.IsActive = false;
        entry.InputNode.Dispose();
        entry.InputNode = null;
    }

    private async Task AttachStreamingInputNodeAsync(StreamingSoundEntry entry)
    {
        if (_graph is null || _output is null) return;

        // Single-attach gate: rapid 0→positive→0→positive slider drags would otherwise spawn
        // two concurrent tasks that both pass the post-await `InputNode is not null` check
        // and both attach a node, leaking the second one onto the graph.
        if (Interlocked.CompareExchange(ref entry.AttachInFlight, 1, 0) != 0) return;

        MediaSource? mediaSource = null;
        MediaSourceAudioInputNode? node = null;
        try
        {
            try
            {
                // Re-resolved on every attach so livestream HLS manifest URLs (~5-6h expiry)
                // stay valid, no timers, no caching.
                mediaSource = await entry.ResolveMediaSource();
            }
            catch (Exception ex)
            {
                DiagnosticLog.Log($"Stream resolve failed for '{entry.Sound.Id}': {ex.Message}");
                StreamAttachFailed?.Invoke(this,
                    $"Couldn't load \"{entry.Sound.DisplayName}\": {ex.Message}");
                return;
            }

            if (!_streamingSounds.ContainsKey(entry.Sound.Id) ||
                !ShouldBePlaying(entry.Volume) ||
                entry.InputNode is not null)
            {
                return;
            }

            CreateMediaSourceAudioInputNodeResult result;
            try
            {
                result = await _graph.CreateMediaSourceAudioInputNodeAsync(mediaSource);
            }
            catch (Exception ex)
            {
                DiagnosticLog.Log($"Streaming node create threw for '{entry.Sound.Id}': {ex.Message}");
                StreamAttachFailed?.Invoke(this,
                    $"Couldn't start \"{entry.Sound.DisplayName}\": {ex.Message}");
                return;
            }

            if (result.Status != MediaSourceAudioInputNodeCreationStatus.Success)
            {
                DiagnosticLog.Log($"Streaming node create failed for '{entry.Sound.Id}': {result.Status} ({result.ExtendedError?.Message})");
                StreamAttachFailed?.Invoke(this,
                    $"Couldn't start \"{entry.Sound.DisplayName}\": {result.Status}");
                return;
            }

            node = result.Node;

            if (!_streamingSounds.ContainsKey(entry.Sound.Id) ||
                !ShouldBePlaying(entry.Volume) ||
                entry.InputNode is not null)
            {
                return;
            }

            node.OutgoingGain = entry.Volume;
            // For finite videos auto-loops indefinitely; for livestreams it's a no-op since
            // they never reach end-of-stream.
            node.LoopCount = int.MaxValue;
            node.AddOutgoingConnection(_output);

            entry.IsActive = true;
            entry.MediaSource = mediaSource;
            entry.InputNode = node;
            // Commit ownership: clear locals so finally doesn't dispose them.
            mediaSource = null;
            node = null;
        }
        finally
        {
            // Dispose order: node first (releases its MediaSource reference), then any
            // standalone MediaSource that wasn't committed.
            node?.Dispose();
            mediaSource?.Dispose();
            Interlocked.Exchange(ref entry.AttachInFlight, 0);
        }
    }

    private static void DetachStreamingInputNode(StreamingSoundEntry entry)
    {
        entry.IsActive = false;
        entry.InputNode?.Dispose();
        entry.InputNode = null;
        entry.MediaSource?.Dispose();
        entry.MediaSource = null;
    }

    private static unsafe void OnQuantumStarted(
        AudioFrameInputNode sender,
        FrameInputNodeQuantumStartedEventArgs args,
        SoundEntry entry)
    {
        // IsActive is the audio-thread/UI-thread sync gate; UI thread sets it false BEFORE
        // disposing the node, see DetachInputNode.
        if (!entry.IsActive) return;

        int requiredSamples = args.RequiredSamples;
        float[] pcm = entry.Pcm;
        if (requiredSamples <= 0 || pcm.Length == 0) return;

        int channels = entry.Channels;
        int totalFloats = requiredSamples * channels;
        uint byteCount = (uint)(totalFloats * sizeof(float));
        var frame = new AudioFrame(byteCount);

        using (var buffer = frame.LockBuffer(AudioBufferAccessMode.Write))
        using (var reference = buffer.CreateReference())
        {
            // Explicit COM QI: the legacy `(IMemoryBufferByteAccess)reference` cast throws
            // InvalidCastException under CsWinRT.
            var byteAccess = reference.As<IMemoryBufferByteAccess>();
            byteAccess.GetBuffer(out byte* dataInBytes, out _);
            float* data = (float*)dataInBytes;

            // Two-chunk MemoryCopy around the loop point, SIMD-vectorized vs per-sample wrap branch.
            long pos = entry.Position;
            int len = pcm.Length;
            int written = 0;
            while (written < totalFloats)
            {
                int chunk = (int)Math.Min(totalFloats - written, len - pos);
                fixed (float* src = &pcm[pos])
                {
                    Buffer.MemoryCopy(
                        src,
                        data + written,
                        (long)chunk * sizeof(float),
                        (long)chunk * sizeof(float));
                }
                written += chunk;
                pos += chunk;
                if (pos >= len) pos = 0;
            }
            entry.Position = pos;

            buffer.Length = byteCount;
        }

        // Closes the rare race where the audio callback ran past the IsActive check just as
        // Detach disposed the node. Two gates together = robust.
        try { sender.AddFrame(frame); }
        catch (ObjectDisposedException) { }
    }

    private static SoundEntry DecodeOgg(Sound sound)
    {
        using var reader = new VorbisReader(sound.SourceUri.LocalPath);
        int channels = reader.Channels;
        int sampleRate = reader.SampleRate;

        long totalInterleaved = reader.TotalSamples * channels;
        if (totalInterleaved <= 0 || totalInterleaved > int.MaxValue)
        {
            throw new InvalidOperationException(
                $"Unexpected sample count {totalInterleaved} for {sound.Id}");
        }

        var pcm = new float[totalInterleaved];
        int totalRead = 0;
        while (totalRead < pcm.Length)
        {
            int read = reader.ReadSamples(pcm, totalRead, pcm.Length - totalRead);
            if (read == 0) break;
            totalRead += read;
        }

        return new SoundEntry
        {
            Sound = sound,
            Pcm = pcm,
            Channels = channels,
            SampleRate = sampleRate,
        };
    }

    private sealed class SoundEntry
    {
        public required Sound Sound { get; init; }
        public required float[] Pcm { get; set; }
        public required int Channels { get; init; }
        public required int SampleRate { get; init; }
        public double Volume;
        public AudioFrameInputNode? InputNode;
        public long Position;
        // Read on the audio thread, written from the UI thread. `volatile` for the JIT memory barrier.
        public volatile bool IsActive;
    }

    private sealed class StreamingSoundEntry
    {
        public required Sound Sound { get; init; }
        // Re-invoked on every attach, the import service knows whether to wrap an HLS manifest
        // in AdaptiveMediaSource or use a direct CreateFromUri, so format choice belongs there.
        public required Func<Task<MediaSource>> ResolveMediaSource { get; init; }
        public double Volume;
        public MediaSourceAudioInputNode? InputNode;
        public MediaSource? MediaSource;
        public volatile bool IsActive;
        // 0 = idle, 1 = attach in progress. Mutated only via Interlocked to enforce
        // exactly-one-attach-in-flight per entry.
        public int AttachInFlight;
    }
}

[ComImport]
[Guid("5B0D3235-4DBA-4D44-865E-8F1D0E4FD04D")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal unsafe interface IMemoryBufferByteAccess
{
    void GetBuffer(out byte* buffer, out uint capacity);
}
