using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace DockBar.Converters;

public class IntEqualsMultiToVisibilityConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values == null || values.Length < 2)
        {
            return Visibility.Collapsed;
        }

        if (values[0] is int a && values[1] is int b)
        {
            return a == b ? Visibility.Visible : Visibility.Collapsed;
        }

        if (int.TryParse(values[0]?.ToString(), out var intA) && int.TryParse(values[1]?.ToString(), out var intB))
        {
            return intA == intB ? Visibility.Visible : Visibility.Collapsed;
        }

        return Visibility.Collapsed;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
