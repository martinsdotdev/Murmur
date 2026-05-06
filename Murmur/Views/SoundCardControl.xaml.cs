using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Murmur.ViewModels;
using Windows.System;

namespace Murmur.Views;

public sealed partial class SoundCardControl : UserControl
{
    public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(
        nameof(ViewModel),
        typeof(SoundCardViewModel),
        typeof(SoundCardControl),
        new PropertyMetadata(null, OnViewModelChanged));

    public SoundCardViewModel? ViewModel
    {
        get => (SoundCardViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    private bool _isActive;
    private bool _isHover;

    public SoundCardControl()
    {
        InitializeComponent();
        Unloaded += (_, _) => DetachViewModel(ViewModel);
        PointerEntered += (_, _) => SetHover(true);
        PointerExited += (_, _) => SetHover(false);
        PointerCanceled += (_, _) => SetHover(false);
        PointerCaptureLost += (_, _) => SetHover(false);
        KeyDown += OnKeyDown;
    }

    // Space is intentionally NOT handled, the page-level "Space = play/pause" accelerator
    // should fire even when a card has focus, matching media-player conventions.
    private void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (ViewModel is null) return;
        if (e.Key == VirtualKey.Enter)
        {
            ViewModel.IsActive = !ViewModel.IsActive;
            e.Handled = true;
        }
    }

    private static void OnViewModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (SoundCardControl)d;
        control.DetachViewModel(e.OldValue as SoundCardViewModel);
        control.AttachViewModel(e.NewValue as SoundCardViewModel);
    }

    private void AttachViewModel(SoundCardViewModel? vm)
    {
        if (vm is null) return;
        vm.PropertyChanged += OnViewModelPropertyChanged;
        _isActive = vm.IsActive;
        UpdateState(useTransitions: false);
        CardRoot.ContextFlyout = CardContextFlyoutBuilder.BuildFor(vm);
    }

    private void DetachViewModel(SoundCardViewModel? vm)
    {
        if (vm is null) return;
        vm.PropertyChanged -= OnViewModelPropertyChanged;
        CardRoot.ContextFlyout = null;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SoundCardViewModel.IsActive) && sender is SoundCardViewModel vm)
        {
            _isActive = vm.IsActive;
            UpdateState();
        }
    }

    private void SetHover(bool hover)
    {
        if (_isHover == hover) return;
        _isHover = hover;
        UpdateState();
    }

    private void UpdateState(bool useTransitions = true)
    {
        var state = (_isActive, _isHover) switch
        {
            (false, false) => "InactiveDefault",
            (false, true)  => "InactiveHover",
            (true,  false) => "ActiveDefault",
            (true,  true)  => "ActiveHover",
        };
        VisualStateManager.GoToState(this, state, useTransitions);
    }

    private void CardRoot_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (ViewModel is null) return;
        if (IsWithinSlider(e.OriginalSource as DependencyObject)) return;
        ViewModel.IsActive = !ViewModel.IsActive;
        e.Handled = true;
    }

    private bool IsWithinSlider(DependencyObject? element)
    {
        while (element is not null)
        {
            if (ReferenceEquals(element, VolumeSlider)) return true;
            element = VisualTreeHelper.GetParent(element);
        }
        return false;
    }
}
