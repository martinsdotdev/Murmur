using Microsoft.UI.Xaml;
using Murmur.Services;
using Murmur.ViewModels;

namespace Murmur;

public partial class App : Application
{
    public static IMixerService Mixer { get; } = new AudioGraphMixerService();

    public static MainViewModel ViewModel { get; set; } = null!;

    public static AppState State { get; set; } = null!;

    public static PresetService PresetService { get; set; } = null!;

    public static TrayService Tray { get; } = new();

    public static SmtcService? Smtc { get; set; }

    public static StartupService Startup { get; } = new();

    public static CustomAudioImportService? ImportService { get; set; }

    public static YouTubeAudioImportService? YouTubeService { get; set; }

    public static Window? MainWindow { get; private set; }

    private Window? _window;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        _window = MainWindow = new MainWindow();
        _window.Activate();
    }
}
