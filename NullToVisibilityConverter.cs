﻿using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace TwitchDeckOverlay.Converter
{
    public class NullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) { return value == null || string.IsNullOrEmpty(value.ToString()) ? Visibility.Visible : Visibility.Collapsed; }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

}