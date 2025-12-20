using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace WinServiceManager.Converters
{
    /// <summary>
    /// 字符串到可见性转换器
    /// </summary>
    public class StringToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string stringValue && !string.IsNullOrWhiteSpace(stringValue))
            {
                bool invert = parameter?.ToString() == "Invert";
                return invert ? Visibility.Collapsed : Visibility.Visible;
            }

            // 如果是 null 或空字符串，根据参数决定返回值
            bool invertDefault = parameter?.ToString() == "Invert";
            return invertDefault ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // 此转换器不需要反向转换
            throw new NotImplementedException();
        }
    }
}