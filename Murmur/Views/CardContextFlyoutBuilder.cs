using Microsoft.UI.Xaml.Controls;
using Murmur.ViewModels;

namespace Murmur.Views;

internal static class CardContextFlyoutBuilder
{
    /// <summary>Returns null for built-ins so right-click is a no-op rather than an empty popup.</summary>
    public static MenuFlyout? BuildFor(SoundCardViewModel vm)
    {
        if (!vm.IsCustom) return null;

        var sound = vm.Sound;
        var flyout = new MenuFlyout();
        var item = new MenuFlyoutItem
        {
            Text = "Remove…",
            Icon = new FontIcon { Glyph = "" },
        };
        item.Click += async (_, _) =>
        {
            if (App.MainWindow is MainWindow mw)
            {
                await mw.RequestDeleteCustomSoundAsync(sound);
            }
        };
        flyout.Items.Add(item);
        return flyout;
    }
}
