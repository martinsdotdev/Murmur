using System;
using System.Collections.Generic;
using System.Linq;
using Murmur.Models;

namespace Murmur.Services;

public sealed class PresetService
{
    private readonly AppState _state;
    private readonly IMixerService _mixer;
    private readonly SoundCatalog _catalog;

    public event EventHandler<Preset>? PresetApplied;

    public PresetService(AppState state, IMixerService mixer, SoundCatalog catalog)
    {
        _state = state;
        _mixer = mixer;
        _catalog = catalog;
    }

    public void Apply(string presetId)
    {
        var preset = _state.Presets.FirstOrDefault(p => p.Id == presetId)
            ?? throw new ArgumentException($"Preset '{presetId}' not found.", nameof(presetId));

        _state.ActivePresetId = presetId;

        foreach (var sound in _catalog.All)
        {
            double v = preset.SoundVolumes.TryGetValue(sound.Id, out var stored) ? stored : 0.0;
            try
            {
                _mixer.SetSoundVolume(sound.Id, v);
            }
            catch (KeyNotFoundException)
            {
                // Catalog has it but mixer doesn't yet (e.g. custom audio not yet restored).
                // Re-apply will pick it up once registered.
            }
        }

        PresetApplied?.Invoke(this, preset);
        _state.RequestSave();
    }

    public Preset SaveCurrentAs(string visibleName)
    {
        var preset = new Preset
        {
            Id = Guid.NewGuid().ToString(),
            VisibleName = string.IsNullOrWhiteSpace(visibleName) ? "Untitled" : visibleName.Trim(),
        };

        foreach (var sound in _catalog.All)
        {
            double v = _mixer.GetSoundVolume(sound.Id);
            if (v > 0) preset.SoundVolumes[sound.Id] = v;
        }

        _state.Presets.Add(preset);
        _state.ActivePresetId = preset.Id;
        PresetApplied?.Invoke(this, preset);
        _state.RequestSave();
        return preset;
    }

    public void Rename(string presetId, string newName)
    {
        var preset = Find(presetId);
        preset.VisibleName = string.IsNullOrWhiteSpace(newName) ? "Untitled" : newName.Trim();
        _state.RequestSave();
    }

    public Preset Duplicate(string presetId)
    {
        var src = Find(presetId);
        var copy = src.DeepClone(Guid.NewGuid().ToString(), src.VisibleName + " copy");
        var index = _state.Presets.IndexOf(src);
        _state.Presets.Insert(index + 1, copy);
        _state.RequestSave();
        return copy;
    }

    public void Delete(string presetId)
    {
        if (presetId == AppState.DefaultPresetId) return;

        var preset = Find(presetId);
        _state.Presets.Remove(preset);

        if (_state.ActivePresetId == presetId)
        {
            Apply(AppState.DefaultPresetId);
        }
        else
        {
            _state.RequestSave();
        }
    }

    public void CycleNext()
    {
        if (_state.Presets.Count <= 1) return;
        var i = IndexOfActive();
        var next = (i + 1) % _state.Presets.Count;
        Apply(_state.Presets[next].Id);
    }

    public void CyclePrev()
    {
        if (_state.Presets.Count <= 1) return;
        var i = IndexOfActive();
        var prev = (i - 1 + _state.Presets.Count) % _state.Presets.Count;
        Apply(_state.Presets[prev].Id);
    }

    /// <summary>Volumes of 0 are removed from the dict, the JSON treats missing as zero.</summary>
    public void UpdateActivePresetVolume(string soundId, double volume)
    {
        if (_state.ActivePreset is not { } preset) return;
        if (volume > 0) preset.SoundVolumes[soundId] = volume;
        else preset.SoundVolumes.Remove(soundId);
        _state.RequestSave();
    }

    private Preset Find(string presetId) =>
        _state.Presets.FirstOrDefault(p => p.Id == presetId)
        ?? throw new ArgumentException($"Preset '{presetId}' not found.", nameof(presetId));

    private int IndexOfActive()
    {
        for (int i = 0; i < _state.Presets.Count; i++)
            if (_state.Presets[i].Id == _state.ActivePresetId) return i;
        return 0;
    }
}
