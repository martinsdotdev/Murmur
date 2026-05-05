using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Dispatching;
using Murmur.Models;

namespace Murmur.Services;

/// <summary>App-wide persisted state at <c>%LOCALAPPDATA%\Murmur\state.json</c>. Atomic-write
/// protocol (write to <c>.tmp</c>, rename over) so a crash mid-write never produces a partial file.</summary>
public sealed partial class AppState : ObservableObject
{
    /// <summary>Same UUID as upstream Blanket so settings could theoretically migrate.</summary>
    public const string DefaultPresetId = "e52f7134-cff9-463b-9f7d-3740d2cc1d57";
    public const int CurrentSchemaVersion = 1;

    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    [ObservableProperty] public partial double MasterVolume { get; set; } = 0.7;
    [ObservableProperty] public partial bool Playing { get; set; }
    [ObservableProperty] public partial bool AutoStart { get; set; }
    [ObservableProperty] public partial bool StartPaused { get; set; }
    [ObservableProperty] public partial bool BackgroundPlayback { get; set; } = true;
    [ObservableProperty] public partial bool InhibitSuspension { get; set; }
    [ObservableProperty] public partial string Theme { get; set; } = "Default";
    [ObservableProperty] public partial bool IsGridView { get; set; }
    partial void OnIsGridViewChanged(bool value) => RequestSave();

    public ObservableCollection<Preset> Presets { get; set; } = new();

    [ObservableProperty] public partial string ActivePresetId { get; set; } = DefaultPresetId;

    public Dictionary<string, string> CustomAudios { get; set; } = new();

    public Dictionary<string, YouTubeAudioRecord> YouTubeAudios { get; set; } = new();

    [JsonIgnore]
    public Preset? ActivePreset => Presets.FirstOrDefault(p => p.Id == ActivePresetId);

    partial void OnActivePresetIdChanged(string value) => OnPropertyChanged(nameof(ActivePreset));

    private CancellationTokenSource? _saveDebounce;
    private readonly SemaphoreSlim _saveLock = new(1, 1);
    private DispatcherQueue? _dispatcher;

    /// <summary>Capture the UI dispatcher so SaveAsync can marshal serialization back —
    /// protects Presets/SoundVolumes/CustomAudios mutations from concurrent enumeration.</summary>
    public void Initialize(DispatcherQueue dispatcher) => _dispatcher = dispatcher;

    /// <summary>Coalesce save requests within <paramref name="delayMs"/> into a single write.</summary>
    public void RequestSave(int delayMs = 500)
    {
        // Atomic CTS swap so old debounces are cancelled and disposed deterministically
        // under heavy slider activity (replaces a prior CTS-leak pattern).
        var fresh = new CancellationTokenSource();
        var prev = Interlocked.Exchange(ref _saveDebounce, fresh);
        prev?.Cancel();
        prev?.Dispose();

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(delayMs, fresh.Token);
                await SaveAsync();
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                DiagnosticLog.Log($"AppState save error: {ex.Message}");
            }
            finally
            {
                if (Interlocked.CompareExchange(ref _saveDebounce, null, fresh) == fresh)
                {
                    fresh.Dispose();
                }
            }
        });
    }

    public async Task SaveAsync()
    {
        // Single-writer queue so two debounces can't collide on the tmp path.
        await _saveLock.WaitAsync();
        try
        {
            // Serialize on the UI thread — Presets / SoundVolumes are mutated from the UI
            // (slider drags), and reflection-based serialization on a background thread can
            // throw "Collection was modified".
            string json = await SerializeOnUIThreadAsync();

            var path = StatePath();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            var tmp = path + ".tmp";
            await File.WriteAllTextAsync(tmp, json);
            File.Move(tmp, path, overwrite: true);
        }
        finally
        {
            _saveLock.Release();
        }
    }

    private Task<string> SerializeOnUIThreadAsync()
    {
        if (_dispatcher is null || _dispatcher.HasThreadAccess)
        {
            return Task.FromResult(JsonSerializer.Serialize(this, s_jsonOptions));
        }

        var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_dispatcher.TryEnqueue(() =>
        {
            try { tcs.SetResult(JsonSerializer.Serialize(this, s_jsonOptions)); }
            catch (Exception ex) { tcs.SetException(ex); }
        }))
        {
            tcs.SetException(new InvalidOperationException("UI dispatcher refused enqueue."));
        }
        return tcs.Task;
    }

    public static async Task<AppState> LoadOrCreateAsync()
    {
        var path = StatePath();
        if (!File.Exists(path)) return CreateDefault();

        try
        {
            await using var stream = File.OpenRead(path);
            var loaded = await JsonSerializer.DeserializeAsync<AppState>(stream, s_jsonOptions);
            if (loaded is null) return CreateDefault();

            if (!loaded.Presets.Any(p => p.Id == DefaultPresetId))
            {
                loaded.Presets.Insert(0, MakeDefaultPreset());
            }
            if (!loaded.Presets.Any(p => p.Id == loaded.ActivePresetId))
            {
                loaded.ActivePresetId = DefaultPresetId;
            }
            return loaded;
        }
        catch (Exception ex)
        {
            DiagnosticLog.Log($"AppState load error (falling back to default): {ex.Message}");
            return CreateDefault();
        }
    }

    private static AppState CreateDefault() => new()
    {
        Presets = new ObservableCollection<Preset> { MakeDefaultPreset() },
        ActivePresetId = DefaultPresetId,
    };

    private static Preset MakeDefaultPreset() => new()
    {
        Id = DefaultPresetId,
        VisibleName = "Default",
        SoundVolumes = new Dictionary<string, double>(),
    };

    private static string StatePath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Murmur",
        "state.json");

    public void RemoveSoundFromAllPresets(string soundId)
    {
        foreach (var preset in Presets)
        {
            preset.SoundVolumes.Remove(soundId);
        }
    }
}
