using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace TwitchDeckOverlay.UI
{
    public class RarityColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int rarityId)
            {
                switch (rarityId)
                {
                    case 1: // Common
                        return new SolidColorBrush(Color.FromRgb(239, 239, 239)); // #FFEFEFEF
                    case 3: // Rare
                        return new SolidColorBrush(Color.FromRgb(0, 183, 235)); // #FF00B7EB
                    case 4: // Epic
                        return new SolidColorBrush(Color.FromRgb(163, 53, 238)); // #FFA335EE
                    case 5: // Legendary
                        return new SolidColorBrush(Color.FromRgb(255, 128, 0)); // #FFFF8000
                    default:
                        return new SolidColorBrush(Color.FromRgb(239, 239, 239)); // #FFEFEFEF
                }
            }
            return new SolidColorBrush(Color.FromRgb(239, 239, 239)); // Default: Common
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}