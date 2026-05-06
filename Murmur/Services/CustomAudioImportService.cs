using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Murmur.Models;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Murmur.Services;

/// <summary>User-imported OGG files copied into <c>%LOCALAPPDATA%\Murmur\CustomAudio\</c>
/// so the import survives a move/delete of the original.</summary>
public sealed class CustomAudioImportService
{
    private readonly IMixerService _mixer;
    private readonly SoundCatalog _catalog;
    private readonly AppState _state;

    public CustomAudioImportService(IMixerService mixer, SoundCatalog catalog, AppState state)
    {
        _mixer = mixer;
        _catalog = catalog;
        _state = state;
    }

    public async Task<Sound?> ImportAsync(Window owner)
    {
        var picker = new FileOpenPicker();
        var hwnd = WindowNative.GetWindowHandle(owner);
        InitializeWithWindow.Initialize(picker, hwnd);
        picker.SuggestedStartLocation = PickerLocationId.MusicLibrary;
        picker.FileTypeFilter.Add(".ogg");

        var storageFile = await picker.PickSingleFileAsync();
        if (storageFile is null) return null;

        var displayName = Path.GetFileNameWithoutExtension(storageFile.Path);
        var id = _catalog.MakeUniqueId(displayName, "custom");

        var customDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Murmur", "CustomAudio");
        Directory.CreateDirectory(customDir);
        var destPath = Path.Combine(customDir, $"{id}.ogg");

        File.Copy(storageFile.Path, destPath, overwrite: false);

        var sound = new Sound(id, displayName, "custom", new Uri(destPath, UriKind.Absolute), SoundKind.CustomOgg);

        try
        {
            await _mixer.RegisterSoundAsync(sound);
        }
        catch (Exception ex)
        {
            DiagnosticLog.Log($"Custom import failed during decode of '{storageFile.Path}': {ex.Message}");
            try { File.Delete(destPath); } catch { }
            throw;
        }

        _catalog.Custom.Add(sound);
        _state.CustomAudios[id] = destPath;
        _state.RequestSave();
        DiagnosticLog.Log($"Imported '{displayName}' as '{id}' → {destPath}");
        return sound;
    }

    public async Task RestoreSavedAsync()
    {
        // Snapshot to allow mutation during iteration.
        var entries = _state.CustomAudios.ToArray();
        foreach (var (id, path) in entries)
        {
            if (!File.Exists(path))
            {
                DiagnosticLog.Log($"Custom audio missing on disk, dropping: {id} ({path})");
                _state.CustomAudios.Remove(id);
                continue;
            }
            var displayName = Path.GetFileNameWithoutExtension(path);
            var sound = new Sound(id, displayName, "custom", new Uri(path, UriKind.Absolute), SoundKind.CustomOgg);
            try
            {
                await _mixer.RegisterSoundAsync(sound);
                _catalog.Custom.Add(sound);
            }
            catch (Exception ex)
            {
                DiagnosticLog.Log($"Custom audio failed to load: {id} - {ex.Message}");
                _state.CustomAudios.Remove(id);
            }
        }
        if (entries.Length != _state.CustomAudios.Count)
        {
            _state.RequestSave();
        }
    }

    public void Delete(Sound sound)
    {
        if (sound.Kind != SoundKind.CustomOgg) return;

        _mixer.UnregisterSound(sound.Id);
        _catalog.Custom.Remove(sound);
        _state.RemoveSoundFromAllPresets(sound.Id);

        if (_state.CustomAudios.TryGetValue(sound.Id, out var path))
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch (Exception ex) { DiagnosticLog.Log($"Custom audio file delete error: {ex.Message}"); }
            _state.CustomAudios.Remove(sound.Id);
        }
        _state.RequestSave();
        DiagnosticLog.Log($"Deleted custom sound '{sound.Id}'.");
    }
}
