using System;
using Microsoft.UI.Xaml.Data;
using Murmur.Helpers;
using Murmur.Models;

namespace Murmur.Converters;

/// <summary>Renders a Preset's active-sound count for the preset list template. Relies on
/// the Preset.SoundVolumes invariant — PresetService removes zero-volume entries on write,
/// so the dictionary count *is* the active count.</summary>
public sealed class PresetSoundCountConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        SoundCountText.Format(value is Preset p ? p.SoundVolumes.Count : 0);

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotImplementedException();
}
