using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace WinServiceManager.Converters
{
    /// <summary>
    /// null 值到可见性转换器
    /// </summary>
    public class NullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isNull = value == null;
            bool invert = parameter?.ToString() == "Invert";

            bool result = invert ? isNull : !isNull;
            return result ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // 此转换器不需要反向转换
            throw new NotImplementedException();
        }
    }
}