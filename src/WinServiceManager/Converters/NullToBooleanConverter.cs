using System;
using System.Globalization;
using System.Windows.Data;

namespace WinServiceManager.Converters
{
    /// <summary>
    /// null 值到布尔值转换器
    /// </summary>
    public class NullToBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isNull = value == null;
            bool invert = parameter?.ToString() == "Invert";

            return invert ? isNull : !isNull;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // 此转换器不需要反向转换
            throw new NotImplementedException();
        }
    }
}