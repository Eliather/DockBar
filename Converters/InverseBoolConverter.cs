using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace DockBar.Converters;

public class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b)
        {
            if (targetType == typeof(Visibility))
            {
                return b ? Visibility.Collapsed : Visibility.Visible;
            }
            return !b;
        }
        return System.Windows.Data.Binding.DoNothing;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b)
        {
            if (targetType == typeof(Visibility))
            {
                return b ? Visibility.Collapsed : Visibility.Visible;
            }
            return !b;
        }
        return System.Windows.Data.Binding.DoNothing;
    }
}
