namespace WinServiceManager.Models
{
    /// <summary>
    /// 启动结果枚举
    /// </summary>
    public enum StartupResult
    {
        /// <summary>
        /// 未知（未启动过）
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// 启动成功
        /// </summary>
        Success = 1,

        /// <summary>
        /// 启动失败
        /// </summary>
        Failed = 2,

        /// <summary>
        /// 警告（启动中或其他警告状态）
        /// </summary>
        Warning = 3
    }
}
