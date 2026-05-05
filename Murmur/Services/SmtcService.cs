using System;
using Microsoft.UI.Dispatching;
using Windows.Media;
using Windows.Media.Playback;

namespace Murmur.Services;

/// <summary>Bridges global media keys / Windows volume overlay to the app via a stub
/// MediaPlayer (CommandManager disabled so its empty state doesn't override our actions).
/// Mirrors Blanket's MPRIS bridge: Next/Previous cycle saved presets.</summary>
public sealed class SmtcService : IDisposable
{
    private readonly DispatcherQueue _uiDispatcher;

    private MediaPlayer? _player;
    private SystemMediaTransportControls? _smtc;

    public Action? OnPlay { get; set; }
    public Action? OnPause { get; set; }
    public Action? OnNext { get; set; }
    public Action? OnPrevious { get; set; }

    public SmtcService(DispatcherQueue uiDispatcher) => _uiDispatcher = uiDispatcher;

    public void Initialize()
    {
        _player = new MediaPlayer();
        _player.CommandManager.IsEnabled = false;

        _smtc = _player.SystemMediaTransportControls;
        _smtc.IsEnabled = true;
        _smtc.IsPlayEnabled = true;
        _smtc.IsPauseEnabled = true;
        _smtc.IsNextEnabled = true;
        _smtc.IsPreviousEnabled = true;
        _smtc.IsStopEnabled = false;
        _smtc.PlaybackStatus = MediaPlaybackStatus.Paused;

        _smtc.DisplayUpdater.Type = MediaPlaybackType.Music;
        _smtc.DisplayUpdater.MusicProperties.Title = "Murmur";
        _smtc.DisplayUpdater.Update();

        _smtc.ButtonPressed += OnButtonPressed;
        DiagnosticLog.Log("SmtcService initialized.");
    }

    public void UpdatePlayState(bool isPlaying)
    {
        if (_smtc is null) return;
        _smtc.PlaybackStatus = isPlaying ? MediaPlaybackStatus.Playing : MediaPlaybackStatus.Paused;
    }

    public void UpdatePresetName(string? presetName)
    {
        if (_smtc is null) return;
        _smtc.DisplayUpdater.MusicProperties.Artist = presetName ?? string.Empty;
        _smtc.DisplayUpdater.Update();
    }

    private void OnButtonPressed(SystemMediaTransportControls sender,
        SystemMediaTransportControlsButtonPressedEventArgs args)
    {
        var button = args.Button;
        DiagnosticLog.Log($"SMTC button: {button}");
        // SMTC fires on a non-UI thread.
        _uiDispatcher.TryEnqueue(() =>
        {
            switch (button)
            {
                case SystemMediaTransportControlsButton.Play: OnPlay?.Invoke(); break;
                case SystemMediaTransportControlsButton.Pause: OnPause?.Invoke(); break;
                case SystemMediaTransportControlsButton.Next: OnNext?.Invoke(); break;
                case SystemMediaTransportControlsButton.Previous: OnPrevious?.Invoke(); break;
            }
        });
    }

    public void Dispose()
    {
        if (_smtc is not null) _smtc.ButtonPressed -= OnButtonPressed;
        _player?.Dispose();
        _player = null;
        _smtc = null;
    }
}
