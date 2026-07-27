using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace FocusPanel.Converters;

public class ProgressToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var progress = value is double d ? d : 0;
        return progress switch
        {
            < 30 => new SolidColorBrush(Color.FromRgb(0xE5, 0x3E, 0x3E)), // red
            < 70 => new SolidColorBrush(Color.FromRgb(0xE5, 0xA5, 0x3E)), // amber
            _    => new SolidColorBrush(Color.FromRgb(0x38, 0xA1, 0x69)), // green
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
