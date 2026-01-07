using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace DockBar.Converters;

public class IntEqualsMultiToVisibilityConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length >= 2 &&
            values[0] is int a &&
            values[1] is int b &&
            a == b)
        {
            return Visibility.Visible;
        }
        return Visibility.Collapsed;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
