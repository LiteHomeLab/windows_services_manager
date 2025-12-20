using System;
using System.Globalization;
using System.Windows.Data;

namespace WinServiceManager.Converters
{
    /// <summary>
    /// 字符串到布尔值转换器
    /// </summary>
    public class StringToBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string stringValue && parameter is string targetValue)
            {
                return string.Equals(stringValue, targetValue, StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue && boolValue && parameter is string targetValue)
            {
                return targetValue;
            }

            return Binding.DoNothing;
        }
    }
}