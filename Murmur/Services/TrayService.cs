using System;
using CommunityToolkit.Mvvm.Input;
using H.NotifyIcon;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;

namespace Murmur.Services;

public sealed class TrayService : IDisposable
{
    private TaskbarIcon? _icon;

    public Action? ShowAction { get; set; }
    public Action? QuitAction { get; set; }
    public Action? TogglePlayAction { get; set; }

    public void Initialize()
    {
        _icon = new TaskbarIcon
        {
            ToolTipText = "Murmur",
            IconSource = new BitmapImage(new Uri("ms-appx:///Assets/AppIcon.ico")),
        };
        _icon.LeftClickCommand = new RelayCommand(() => ShowAction?.Invoke());
        _icon.DoubleClickCommand = new RelayCommand(() => ShowAction?.Invoke());
        _icon.ContextFlyout = BuildContextMenu();
        _icon.ForceCreate();
        DiagnosticLog.Log("TrayService initialized.");
    }

    public void UpdateTooltip(string text)
    {
        if (_icon is not null) _icon.ToolTipText = text;
    }

    public void Dispose()
    {
        _icon?.Dispose();
        _icon = null;
    }

    private MenuFlyout BuildContextMenu()
    {
        var flyout = new MenuFlyout();

        var show = new MenuFlyoutItem { Text = "Show Murmur" };
        show.Click += (_, _) => ShowAction?.Invoke();
        flyout.Items.Add(show);

        var togglePlay = new MenuFlyoutItem { Text = "Play / Pause" };
        togglePlay.Click += (_, _) => TogglePlayAction?.Invoke();
        flyout.Items.Add(togglePlay);

        flyout.Items.Add(new MenuFlyoutSeparator());

        var quit = new MenuFlyoutItem { Text = "Quit" };
        quit.Click += (_, _) => QuitAction?.Invoke();
        flyout.Items.Add(quit);

        return flyout;
    }
}
