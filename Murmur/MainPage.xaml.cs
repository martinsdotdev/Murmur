using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Murmur.ViewModels;

namespace Murmur;

public sealed partial class MainPage : Page
{
    public MainViewModel ViewModel { get; }

    /// <summary>Mirror of <see cref="MainViewModel.SoundCards"/> filtered by the
    /// search box. Bound by the tile <see cref="ItemsRepeater"/>.</summary>
    public ObservableCollection<SoundCardViewModel> FilteredSoundCards { get; } = new();

    private string _searchQuery = string.Empty;

    public MainPage()
    {
        ViewModel = App.ViewModel;
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await ViewModel.InitializeAsync();
            ViewModel.SoundCards.CollectionChanged += OnSoundCardsChanged;
            RebuildFilteredCards();
        }
        catch (Exception ex)
        {
            ViewModel.StatusMessage = $"Initialization failed: {ex.Message}";
        }
    }

    // WinUI's HorizontalAlignment=Center sizes a Grid to content, so MaxWidth alone won't
    // cap width — set Width explicitly so header and rows share the same gutter at every size.
    private const double ContentMaxWidth = 880;
    private void Page_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        ContentRoot.Width = Math.Min(ContentMaxWidth, e.NewSize.Width);
    }

    private async void AddFromFile_Click(object sender, RoutedEventArgs e)
    {
        if (App.MainWindow is not Window owner) return;
        var sound = await ViewModel.ImportCustomAudioAsync(owner);
        if (sound is not null)
        {
            ViewModel.StatusMessage = $"Imported \"{sound.DisplayName}\". Drag its slider above 0 to mix it in.";
        }
    }

    private async void AddFromUrl_Click(object sender, RoutedEventArgs e)
    {
        if (App.MainWindow is not MainWindow win) return;
        var url = await win.PromptForUrlAsync("Add a YouTube sound");
        if (string.IsNullOrWhiteSpace(url)) return;
        await ViewModel.ImportYouTubeAudioAsync(url);
    }

    // Stubs for an in-progress filter feature. The XAML ItemsRepeaters bind to
    // ViewModel.SoundCards directly today; FilteredSoundCards is wired up when
    // the search box gets added back. Keep the property + handlers in place so
    // OnLoaded can subscribe and the future search box can call Rebuild.
    private void OnSoundCardsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RebuildFilteredCards();
    }

    private void RebuildFilteredCards()
    {
        FilteredSoundCards.Clear();
        var query = _searchQuery.Trim();
        foreach (var card in ViewModel.SoundCards)
        {
            if (query.Length == 0 ||
                card.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                FilteredSoundCards.Add(card);
            }
        }
    }
}
