using System.Globalization;
using Avalonia.Data.Converters;
using simpleRestriper.Models;

namespace simpleRestriper.Converters;

public class StatusToSymbolConverter : IValueConverter
{
    public static readonly StatusToSymbolConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is RestripeStatus status)
        {
            return status switch
            {
                RestripeStatus.Pending => "",
                RestripeStatus.Processing => "...",
                RestripeStatus.Success => "✓",
                RestripeStatus.Error => "✗",
                _ => ""
            };
        }
        return "";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class StatusToColorConverter : IValueConverter
{
    public static readonly StatusToColorConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is RestripeStatus status)
        {
            return status switch
            {
                RestripeStatus.Success => Avalonia.Media.Brushes.Green,
                RestripeStatus.Error => Avalonia.Media.Brushes.Red,
                RestripeStatus.Processing => Avalonia.Media.Brushes.Blue,
                _ => Avalonia.Media.Brushes.Gray
            };
        }
        return Avalonia.Media.Brushes.Gray;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
