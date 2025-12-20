using System;

namespace WinServiceManager.Models
{
    /// <summary>
    /// 性能统计数据
    /// </summary>
    public class PerformanceStats
    {
        /// <summary>
        /// 文件监视器数量
        /// </summary>
        public int FileWatcherCount { get; set; }

        /// <summary>
        /// 活跃订阅数量
        /// </summary>
        public int SubscriptionCount { get; set; }

        /// <summary>
        /// 每分钟操作数
        /// </summary>
        public int OperationsPerMinute { get; set; }

        /// <summary>
        /// 内存使用量（MB）
        /// </summary>
        public long MemoryUsageMB { get; set; }

        /// <summary>
        /// CPU使用率（百分比）
        /// </summary>
        public double CpuUsagePercent { get; set; }

        /// <summary>
        /// 活跃句柄数
        /// </summary>
        public int ActiveHandles { get; set; }

        /// <summary>
        /// 最后更新时间
        /// </summary>
        public DateTime LastUpdated { get; set; } = DateTime.Now;
    }
}