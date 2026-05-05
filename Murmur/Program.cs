using System;
using System.Threading;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;

namespace Murmur;

/// <summary>Custom entry point with single-instance redirection — second launches hand
/// activation args to the first instance and exit. Generated <c>Main</c> is suppressed
/// via <c>DISABLE_XAML_GENERATED_MAIN</c> in csproj.</summary>
public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        WinRT.ComWrappersSupport.InitializeComWrappers();

        bool isRedirect = DecideRedirection();
        if (isRedirect) return 0;

        Application.Start(p =>
        {
            var context = new DispatcherQueueSynchronizationContext(
                DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(context);
            _ = new App();
        });
        return 0;
    }

    private static bool DecideRedirection()
    {
        var activationArgs = AppInstance.GetCurrent().GetActivatedEventArgs();
        var keyInstance = AppInstance.FindOrRegisterForKey("Murmur-singleinstance");

        if (keyInstance.IsCurrent)
        {
            keyInstance.Activated += OnActivated;
            return false;
        }

        keyInstance.RedirectActivationToAsync(activationArgs).AsTask().Wait();
        return true;
    }

    private static void OnActivated(object? sender, AppActivationArguments args)
    {
        var window = App.MainWindow;
        if (window is null) return;

        window.DispatcherQueue.TryEnqueue(() =>
        {
            if (App.MainWindow is { } w)
            {
                w.AppWindow?.Show();
                w.Activate();
            }
        });
    }
}
