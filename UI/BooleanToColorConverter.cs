using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using System;

public class BooleanToColorConverter : IValueConverter
{
    public string TrueColor { get; set; }
    public string FalseColor { get; set; }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isConnected)
        {
            return isConnected ? new SolidColorBrush((Color)ColorConverter.ConvertFromString(TrueColor)) : new SolidColorBrush((Color)ColorConverter.ConvertFromString(FalseColor));
        }
        return new SolidColorBrush(Colors.Red);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}