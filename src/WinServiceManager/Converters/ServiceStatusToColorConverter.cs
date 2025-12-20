using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using WinServiceManager.Models;

namespace WinServiceManager.Converters
{
    /// <summary>
    /// 服务状态到颜色转换器
    /// </summary>
    public class ServiceStatusToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ServiceStatus status)
            {
                return status switch
                {
                    ServiceStatus.Running => new SolidColorBrush(Color.FromRgb(76, 175, 80)),     // Green
                    ServiceStatus.Stopped => new SolidColorBrush(Color.FromRgb(244, 67, 54)),      // Red
                    ServiceStatus.Starting => new SolidColorBrush(Color.FromRgb(255, 152, 0)),    // Orange
                    ServiceStatus.Stopping => new SolidColorBrush(Color.FromRgb(255, 152, 0)),    // Orange
                    ServiceStatus.Installing => new SolidColorBrush(Color.FromRgb(156, 39, 176)),  // Purple
                    ServiceStatus.Uninstalling => new SolidColorBrush(Color.FromRgb(156, 39, 176)), // Purple
                    ServiceStatus.Error => new SolidColorBrush(Color.FromRgb(183, 28, 28)),       // Dark Red
                    _ => new SolidColorBrush(Color.FromRgb(158, 158, 158))                       // Gray
                };
            }

            // 默认返回灰色
            return new SolidColorBrush(Color.FromRgb(158, 158, 158));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // 此转换器不需要反向转换
            throw new NotImplementedException();
        }
    }
}