using System;
using System.Globalization;
using System.Windows.Data;

namespace WinServiceManager.Converters
{
    /// <summary>
    /// 文件大小转换器
    /// </summary>
    public class FileSizeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is long bytes)
            {
                return FormatFileSize(bytes);
            }

            if (value is double doubleBytes)
            {
                return FormatFileSize((long)doubleBytes);
            }

            return "0 B";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        private static string FormatFileSize(long bytes)
        {
            const long KB = 1024;
            const long MB = KB * 1024;
            const long GB = MB * 1024;
            const long TB = GB * 1024;

            if (bytes < KB)
                return $"{bytes} B";
            else if (bytes < MB)
                return $"{bytes / (double)KB:F1} KB";
            else if (bytes < GB)
                return $"{bytes / (double)MB:F1} MB";
            else if (bytes < TB)
                return $"{bytes / (double)GB:F1} GB";
            else
                return $"{bytes / (double)TB:F1} TB";
        }
    }
}