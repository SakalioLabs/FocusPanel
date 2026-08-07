using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace FocusPanel.Converters;

public class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool booleanValue)
        {
            bool inverted = !booleanValue;
            return targetType == typeof(Visibility)
                ? inverted
                    ? Visibility.Visible
                    : Visibility.Collapsed
                : inverted;
        }
        return value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool booleanValue)
        {
            return !booleanValue;
        }
        if (value is Visibility visibility)
        {
            return visibility
                != Visibility.Visible;
        }
        return value;
    }
}
