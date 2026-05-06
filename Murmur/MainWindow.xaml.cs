using System;
using System.Threading.Tasks;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using Murmur.Models;
using Murmur.Services;
using Murmur.ViewModels;

namespace Murmur;

public sealed partial class MainWindow : Window
{
    public MainViewModel ViewModel { get; }

    public MainWindow()
    {
        // Construct VM before InitializeComponent so x:Bind expressions in the TitleBar
        // evaluate against a real instance instead of null.
        ViewModel = new MainViewModel(App.Mixer);
        App.ViewModel = ViewModel;

        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.SetIcon("Assets/AppIcon.ico");

        // Tall caption buttons span the full TitleBar height instead of floating as a
        // short capsule top-right. The TitleBar control doesn't auto-set this.
        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;

        // Below ~480 wide the row's fixed name column crowds the slider; below ~560 tall
        // fewer than 6 rows are reachable without scrolling.
        if (AppWindow.Presenter is OverlappedPresenter op)
        {
            op.PreferredMinimumWidth = 480;
            op.PreferredMinimumHeight = 560;
        }

        AppWindow.Resize(new Windows.Graphics.SizeInt32(540, 640));

        App.Tray.ShowAction = ShowFromTray;
        App.Tray.QuitAction = QuitApp;
        App.Tray.TogglePlayAction = TogglePlay;
        App.Tray.Initialize();

        var smtc = new SmtcService(Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread());
        smtc.OnPlay = SafePlay;
        smtc.OnPause = SafePause;
        smtc.OnNext = () => App.PresetService?.CycleNext();
        smtc.OnPrevious = () => App.PresetService?.CyclePrev();
        smtc.Initialize();
        App.Smtc = smtc;

        Closed += OnWindowClosed;
        Activated += OnWindowActivated;

        RootFrame.Navigated += OnFrameNavigated;
        RootFrame.Navigate(typeof(MainPage), ViewModel);
    }

    private void OnWindowActivated(object sender, WindowActivatedEventArgs args)
    {
        AppTitleBar.Opacity = args.WindowActivationState == WindowActivationState.Deactivated ? 0.6 : 1.0;
    }

    private void OnFrameNavigated(object sender, NavigationEventArgs e)
    {
        var isSubpage = e.SourcePageType != typeof(MainPage);
        PresetChip.Visibility = isSubpage ? Visibility.Collapsed : Visibility.Visible;
        VolumeButton.Visibility = isSubpage ? Visibility.Collapsed : Visibility.Visible;
        OverflowButton.Visibility = isSubpage ? Visibility.Collapsed : Visibility.Visible;
        PlaybackFooter.Visibility = isSubpage ? Visibility.Collapsed : Visibility.Visible;
        AppTitleBar.Subtitle = e.SourcePageType == typeof(Views.SettingsPage) ? "Preferences" : "";
        // Frame.CanGoBack doesn't raise PropertyChanged, so the XAML x:Bind for
        // IsBackButtonVisible would only ever see the initial value, push it manually here.
        AppTitleBar.IsBackButtonVisible = RootFrame.CanGoBack;
    }

    private void AppTitleBar_BackRequested(TitleBar sender, object args)
    {
        if (RootFrame.CanGoBack) RootFrame.GoBack();
    }

    private void GridViewToggle_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.IsGridView = !ViewModel.IsGridView;
    }

    // Mixer-only shortcuts gate on IsOnMixerPage so they don't mutate hidden mixer state
    // from a subpage. Space (play/pause) and Ctrl+P (Settings) stay window-scoped.
    private bool IsOnMixerPage => RootFrame.CurrentSourcePageType == typeof(MainPage);

    private void PlayPauseAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        if (ViewModel.PlayPauseCommand.CanExecute(null))
            ViewModel.PlayPauseCommand.Execute(null);
    }

    private async void ResetAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        if (!IsOnMixerPage) return;
        await RequestResetMixAsync();
    }

    private async void ImportAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        if (!IsOnMixerPage) return;
        await RequestImportAsync();
    }

    private void GridViewAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        if (!IsOnMixerPage) return;
        ViewModel.IsGridView = !ViewModel.IsGridView;
    }

    private void SettingsAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        RequestOpenSettings();
    }

    private void TogglePlay()
    {
        if (App.Mixer.IsPlaying) SafePause();
        else SafePlay();
    }

    // Tray icon and SMTC come up before InitializeAsync wires the AudioGraph, so a click
    // in that window must no-op rather than throw "Mixer not initialized".
    private static void SafePlay()
    {
        try { App.Mixer.Play(); }
        catch (InvalidOperationException) { }
    }

    private static void SafePause()
    {
        try { App.Mixer.Pause(); }
        catch (InvalidOperationException) { }
    }

    private void OnWindowClosed(object sender, WindowEventArgs args)
    {
        if (App.State?.BackgroundPlayback == true)
        {
            args.Handled = true;
            AppWindow.Hide();
            DiagnosticLog.Log("Window hidden to tray.");
            return;
        }

        // Real close: flush pending state synchronously to avoid losing the last 500 ms
        // of slider drags. UI-thread block acceptable during shutdown.
        try { App.State?.SaveAsync().GetAwaiter().GetResult(); }
        catch (Exception ex) { DiagnosticLog.Log($"Close save error: {ex.Message}"); }

        App.Smtc?.Dispose();
        try
        {
            if (App.Mixer is IAsyncDisposable mixer)
                mixer.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
        catch (Exception ex) { DiagnosticLog.Log($"Close mixer dispose error: {ex.Message}"); }

        ViewModel.Dispose();
        App.Tray.Dispose();
    }

    private void ShowFromTray()
    {
        AppWindow.Show();
        Activate();
    }

    private async void QuitApp()
    {
        try { if (App.State is not null) await App.State.SaveAsync(); }
        catch (Exception ex) { DiagnosticLog.Log($"Quit save error: {ex.Message}"); }

        App.Smtc?.Dispose();
        if (App.Mixer is IAsyncDisposable mixer)
        {
            try { await mixer.DisposeAsync(); }
            catch (Exception ex) { DiagnosticLog.Log($"Quit mixer dispose error: {ex.Message}"); }
        }

        ViewModel.Dispose();
        App.Tray.Dispose();
        Application.Current.Exit();
    }

    private void PresetFlyout_Opening(object sender, object e)
    {
        PresetFlyout.Items.Clear();

        foreach (var preset in ViewModel.Presets)
        {
            var captured = preset;
            var item = new ToggleMenuFlyoutItem
            {
                Text = preset.VisibleName,
                IsChecked = ReferenceEquals(preset, ViewModel.ActivePreset),
            };
            item.Click += (_, _) => ViewModel.ActivePreset = captured;
            PresetFlyout.Items.Add(item);
        }

        if (PresetFlyout.Items.Count > 0)
            PresetFlyout.Items.Add(new MenuFlyoutSeparator());

        var saveAs = new MenuFlyoutItem
        {
            Text = "Save current as preset…",
            Icon = new FontIcon { Glyph = "" },
        };
        saveAs.Click += SaveAsNew_Click;
        PresetFlyout.Items.Add(saveAs);

        var active = ViewModel.ActivePreset;
        if (active is not null && active.Id != AppState.DefaultPresetId)
        {
            PresetFlyout.Items.Add(new MenuFlyoutSeparator());

            var rename = new MenuFlyoutItem
            {
                Text = "Rename current…",
                Icon = new FontIcon { Glyph = "" },
            };
            rename.Click += Rename_Click;
            PresetFlyout.Items.Add(rename);

            var dupe = new MenuFlyoutItem
            {
                Text = "Duplicate current",
                Icon = new FontIcon { Glyph = "" },
            };
            dupe.Click += Duplicate_Click;
            PresetFlyout.Items.Add(dupe);

            var del = new MenuFlyoutItem
            {
                Text = "Delete current",
                Icon = new FontIcon { Glyph = "" },
            };
            del.Click += Delete_Click;
            PresetFlyout.Items.Add(del);
        }
    }

    private async void SaveAsNew_Click(object sender, RoutedEventArgs e)
    {
        var name = await PromptForNameAsync("Save current mix as new preset", "New preset");
        if (string.IsNullOrWhiteSpace(name)) return;
        ViewModel.SaveCurrentAsNewPreset(name);
    }

    private async void Rename_Click(object sender, RoutedEventArgs e)
    {
        var current = ViewModel.ActivePreset?.VisibleName ?? "";
        var name = await PromptForNameAsync("Rename preset", current);
        if (string.IsNullOrWhiteSpace(name) || name == current) return;
        ViewModel.RenameActivePreset(name);
    }

    private void Duplicate_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.DuplicateActivePreset();
    }

    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        var preset = ViewModel.ActivePreset;
        if (preset is null) return;
        if (preset.Id == AppState.DefaultPresetId)
        {
            await ShowMessageAsync("Can't delete the default preset",
                "The default preset is always kept as a fallback. You can rename it, but not delete it.");
            return;
        }
        var ok = await ConfirmAsync("Delete preset?",
            $"\"{preset.VisibleName}\" will be removed permanently. The default preset will become active.");
        if (ok) ViewModel.DeleteActivePreset();
    }

    private async void ResetMix_Click(object sender, RoutedEventArgs e) => await RequestResetMixAsync();

    private async void Import_Click(object sender, RoutedEventArgs e) => await RequestImportAsync();

    private void Settings_Click(object sender, RoutedEventArgs e) => RequestOpenSettings();

    internal async Task RequestResetMixAsync()
    {
        if (ViewModel.ActiveSoundCount == 0) return;
        var ok = await ConfirmAsync("Reset mix?",
            "Every sound's volume will be set to 0. The current preset will be updated.");
        if (ok) ViewModel.ResetMix();
    }

    internal async Task RequestImportAsync()
    {
        var sound = await ViewModel.ImportCustomAudioAsync(this);
        if (sound is not null)
        {
            ViewModel.StatusMessage = $"Imported \"{sound.DisplayName}\". Drag its slider above 0 to mix it in.";
        }
    }

    internal async Task RequestDeleteCustomSoundAsync(Sound sound)
    {
        if (sound.Kind == SoundKind.Streaming)
        {
            if (App.YouTubeService is null) return;
            var ok = await ConfirmAsync(
                $"Remove \"{sound.DisplayName}\"?",
                "This YouTube sound will be removed from your mixer.");
            if (!ok) return;
            App.YouTubeService.Delete(sound);
        }
        else
        {
            if (App.ImportService is null) return;
            var ok = await ConfirmAsync(
                $"Remove \"{sound.DisplayName}\"?",
                "The .ogg file will be permanently deleted.");
            if (!ok) return;
            App.ImportService.Delete(sound);
        }
        ViewModel.StatusMessage = $"Removed \"{sound.DisplayName}\".";
    }

    internal void RequestOpenSettings()
    {
        if (RootFrame.CurrentSourcePageType == typeof(Views.SettingsPage)) return;
        RootFrame.Navigate(typeof(Views.SettingsPage), ViewModel, new DrillInNavigationTransitionInfo());
    }

    private async Task<string?> PromptForNameAsync(string title, string initialText)
    {
        if (Content?.XamlRoot is not { } root) return null;
        var textBox = new TextBox
        {
            Text = initialText,
            SelectionStart = initialText.Length,
            MinWidth = 320,
        };
        var dialog = new ContentDialog
        {
            Title = title,
            Content = textBox,
            PrimaryButtonText = "OK",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = root,
        };
        var result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary ? textBox.Text.Trim() : null;
    }

    internal async Task<string?> PromptForUrlAsync(string title)
    {
        if (Content?.XamlRoot is not { } root) return null;

        // Reuse the import-time validator so a spoofed string like "https://evil.com/?r=youtube.com/"
        // doesn't make it into the dialog. Clipboard access can throw, silent on failure.
        string prefill = string.Empty;
        try
        {
            var clip = Windows.ApplicationModel.DataTransfer.Clipboard.GetContent();
            if (clip.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.Text))
            {
                var text = (await clip.GetTextAsync()).Trim();
                if (YouTubeAudioImportService.IsAcceptableYouTubeUrl(text, out var canonical))
                {
                    prefill = canonical;
                }
            }
        }
        catch (Exception ex) { DiagnosticLog.Log($"Clipboard prefill skipped: {ex.Message}"); }

        var textBox = new TextBox
        {
            PlaceholderText = "https://www.youtube.com/watch?v=…",
            Text = prefill,
            SelectionStart = prefill.Length,
            MinWidth = 380,
        };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(textBox, "YouTube URL");

        var dialog = new ContentDialog
        {
            Title = title,
            Content = textBox,
            PrimaryButtonText = "Add",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = root,
        };
        var result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary ? textBox.Text.Trim() : null;
    }

    private async Task<bool> ConfirmAsync(string title, string message)
    {
        if (Content?.XamlRoot is not { } root) return false;
        var dialog = new ContentDialog
        {
            Title = title,
            Content = message,
            PrimaryButtonText = "OK",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = root,
        };
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    private async Task ShowMessageAsync(string title, string message)
    {
        if (Content?.XamlRoot is not { } root) return;
        var dialog = new ContentDialog
        {
            Title = title,
            Content = message,
            CloseButtonText = "OK",
            XamlRoot = root,
        };
        await dialog.ShowAsync();
    }
}
