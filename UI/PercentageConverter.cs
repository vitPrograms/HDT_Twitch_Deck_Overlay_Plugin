using System;
using System.Globalization;
using System.Windows.Data;

namespace TwitchDeckOverlay.UI
{
    public class PercentageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double baseValue && parameter is string percentageStr && double.TryParse(percentageStr, out double percentage))
            {
                return baseValue * (percentage / 100.0);
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}