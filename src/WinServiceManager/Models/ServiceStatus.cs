namespace WinServiceManager.Models
{
    /// <summary>
    /// 服务状态枚举
    /// </summary>
    public enum ServiceStatus
    {
        /// <summary>
        /// 未安装
        /// </summary>
        NotInstalled = 0,

        /// <summary>
        /// 已停止
        /// </summary>
        Stopped = 1,

        /// <summary>
        /// 正在运行
        /// </summary>
        Running = 2,

        /// <summary>
        /// 正在启动
        /// </summary>
        Starting = 3,

        /// <summary>
        /// 正在停止
        /// </summary>
        Stopping = 4,

        /// <summary>
        /// 已暂停
        /// </summary>
        Paused = 5,

        /// <summary>
        /// 错误状态
        /// </summary>
        Error = 6,

        /// <summary>
        /// 正在安装
        /// </summary>
        Installing = 7,

        /// <summary>
        /// 正在卸载
        /// </summary>
        Uninstalling = 8
    }

    /// <summary>
    /// ServiceStatus 扩展方法
    /// </summary>
    public static class ServiceStatusExtensions
    {
        /// <summary>
        /// 获取状态的显示文本
        /// </summary>
        public static string GetDisplayText(this ServiceStatus status)
        {
            return status switch
            {
                ServiceStatus.NotInstalled => "未安装",
                ServiceStatus.Stopped => "已停止",
                ServiceStatus.Running => "运行中",
                ServiceStatus.Starting => "启动中",
                ServiceStatus.Stopping => "停止中",
                ServiceStatus.Paused => "已暂停",
                ServiceStatus.Error => "错误",
                ServiceStatus.Installing => "安装中",
                ServiceStatus.Uninstalling => "卸载中",
                _ => "未知"
            };
        }

        /// <summary>
        /// 判断是否为中间状态
        /// </summary>
        public static bool IsTransitioning(this ServiceStatus status)
        {
            return status switch
            {
                ServiceStatus.Starting or
                ServiceStatus.Stopping or
                ServiceStatus.Installing or
                ServiceStatus.Uninstalling => true,
                _ => false
            };
        }

        /// <summary>
        /// 判断是否可以进行启动操作
        /// </summary>
        public static bool CanStart(this ServiceStatus status)
        {
            return status == ServiceStatus.Stopped || status == ServiceStatus.NotInstalled;
        }

        /// <summary>
        /// 判断是否可以进行停止操作
        /// </summary>
        public static bool CanStop(this ServiceStatus status)
        {
            return status == ServiceStatus.Running || status == ServiceStatus.Starting;
        }

        /// <summary>
        /// 判断是否可以进行卸载操作
        /// </summary>
        public static bool CanUninstall(this ServiceStatus status)
        {
            return status == ServiceStatus.Stopped || status == ServiceStatus.NotInstalled;
        }
    }
}