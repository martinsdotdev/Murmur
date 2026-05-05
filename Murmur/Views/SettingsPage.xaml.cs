using System;
using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using Murmur.ViewModels;
using Windows.ApplicationModel;
using Windows.System;

namespace Murmur.Views;

public sealed partial class SettingsPage : Page
{
    private const string RepositoryUri = "https://github.com/martinsdotdev/Murmur";
    private const double ContentMaxWidth = 720;

    public MainViewModel ViewModel { get; private set; } = null!;

    public SettingsPage()
    {
        InitializeComponent();
        VersionText.Text = $"v{ReadVersion()}";
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        ViewModel = e.Parameter as MainViewModel ?? App.ViewModel;
        Bindings.Update();
    }

    private void Page_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        ContentRoot.Width = Math.Min(ContentMaxWidth, e.NewSize.Width);
    }

    private async void OpenRepository_Click(object sender, RoutedEventArgs e)
    {
        await Launcher.LaunchUriAsync(new Uri(RepositoryUri));
    }

    private void EscapeAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (Frame is { CanGoBack: true } frame)
        {
            frame.GoBack();
            args.Handled = true;
        }
    }

    private static string ReadVersion()
    {
        try
        {
            var v = Package.Current.Id.Version;
            return string.Create(CultureInfo.InvariantCulture, $"{v.Major}.{v.Minor}.{v.Build}");
        }
        catch
        {
            return typeof(SettingsPage).Assembly.GetName().Version?.ToString(3) ?? "—";
        }
    }
}
