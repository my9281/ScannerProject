using System.Globalization;

namespace Scanner.MaUI.Converters;

public sealed class InvertedBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value is bool state && !state;
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => value is bool state && !state;
}
