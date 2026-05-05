using System;
using System.Globalization;
using Microsoft.UI.Xaml.Data;

namespace Murmur.Converters;

/// <summary>Renders a 0..1 slider value as a 0..100 integer for the slider thumb tooltip
/// — WinUI's default formats doubles to many decimals, surfacing FP trailing zeros while
/// dragging.</summary>
public sealed class VolumePercentTooltipConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var v = value switch
        {
            double d => d,
            _ => 0.0,
        };
        return ((int)Math.Round(v * 100)).ToString(CultureInfo.InvariantCulture);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotImplementedException();
}
