using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WinServiceManager.Models;

namespace WinServiceManager.Services
{
    /// <summary>
    /// 性能监控服务
    /// </summary>
    public interface IPerformanceMonitorService
    {
        /// <summary>
        /// 获取当前性能统计
        /// </summary>
        PerformanceStats GetCurrentStats();

        /// <summary>
        /// 获取历史性能数据
        /// </summary>
        /// <param name="duration">持续时间</param>
        /// <returns>性能数据列表</returns>
        Task<List<PerformanceStats>> GetHistoricalStatsAsync(TimeSpan duration);

        /// <summary>
        /// 开始监控
        /// </summary>
        void StartMonitoring();

        /// <summary>
        /// 停止监控
        /// </summary>
        void StopMonitoring();
    }

    /// <summary>
    /// 性能监控配置
    /// </summary>
    public class PerformanceMonitorOptions
    {
        /// <summary>
        /// 监控间隔（毫秒）
        /// </summary>
        public int IntervalMs { get; set; } = 5000;

        /// <summary>
        /// 保留历史数据数量
        /// </summary>
        public int MaxHistoryCount { get; set; } = 1000;

        /// <summary>
        /// CPU使用率过高阈值（百分比）
        /// </summary>
        public double CpuHighThreshold { get; set; } = 80;

        /// <summary>
        /// 内存使用过高阈值（MB）
        /// </summary>
        public long MemoryHighThresholdMB { get; set; } = 512;
    }

    /// <summary>
    /// 性能监控服务实现
    /// </summary>
    public class PerformanceMonitorService : IPerformanceMonitorService, IDisposable
    {
        private readonly ILogger<PerformanceMonitorService> _logger;
        private readonly PerformanceMonitorOptions _options;
        private readonly Timer _monitorTimer;
        private readonly LinkedList<PerformanceStats> _history;
        private readonly object _historyLock = new object();
        private readonly PerformanceCounter? _cpuCounter;
        private Process _currentProcess;
        private bool _isMonitoring = false;
        private DateTime _lastCpuTime = DateTime.MinValue;
        private TimeSpan _lastProcessorTime = TimeSpan.Zero;

        public PerformanceMonitorService(
            ILogger<PerformanceMonitorService> logger,
            IOptions<PerformanceMonitorOptions>? options = null)
        {
            _logger = logger;
            _options = options?.Value ?? new PerformanceMonitorOptions();
            _history = new LinkedList<PerformanceStats>();
            _currentProcess = Process.GetCurrentProcess();

            // 初始化CPU计数器
            try
            {
                _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to initialize CPU performance counter");
            }

            _monitorTimer = new Timer(CollectMetrics, null, Timeout.Infinite, Timeout.Infinite);
        }

        public PerformanceStats GetCurrentStats()
        {
            var stats = new PerformanceStats();

            try
            {
                // 获取当前进程信息
                _currentProcess.Refresh();
                stats.MemoryUsageMB = _currentProcess.WorkingSet64 / 1024 / 1024;
                stats.ActiveHandles = _currentProcess.HandleCount;

                // 获取CPU使用率
                stats.CpuUsagePercent = GetCurrentCpuUsage();

                stats.LastUpdated = DateTime.Now;

                return stats;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error collecting performance stats");
                return stats;
            }
        }

        private double GetCurrentCpuUsage()
        {
            try
            {
                if (_cpuCounter != null)
                {
                    return _cpuCounter.NextValue();
                }
                else
                {
                    // 使用进程时间计算CPU使用率
                    var currentTime = DateTime.Now;
                    var currentProcessorTime = _currentProcess.TotalProcessorTime;

                    if (_lastCpuTime != DateTime.MinValue)
                    {
                        var timeDiff = currentTime - _lastCpuTime;
                        var processorTimeDiff = currentProcessorTime - _lastProcessorTime;
                        var cpuUsage = (processorTimeDiff.TotalMilliseconds / timeDiff.TotalMilliseconds) * 100;
                        return Math.Min(cpuUsage, 100);
                    }

                    _lastCpuTime = currentTime;
                    _lastProcessorTime = currentProcessorTime;
                    return 0;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get CPU usage");
                return 0;
            }
        }

        public async Task<List<PerformanceStats>> GetHistoricalStatsAsync(TimeSpan duration)
        {
            return await Task.Run(() =>
            {
                lock (_historyLock)
                {
                    var cutoff = DateTime.Now.Subtract(duration);
                    return _history
                        .Where(s => s.LastUpdated >= cutoff)
                        .ToList();
                }
            });
        }

        public void StartMonitoring()
        {
            if (_isMonitoring)
                return;

            _isMonitoring = true;
            _monitorTimer.Change(0, _options.IntervalMs);
            _logger.LogInformation($"Performance monitoring started with interval: {_options.IntervalMs}ms");
        }

        public void StopMonitoring()
        {
            if (!_isMonitoring)
                return;

            _isMonitoring = false;
            _monitorTimer.Change(Timeout.Infinite, Timeout.Infinite);
            _logger.LogInformation("Performance monitoring stopped");
        }

        private void CollectMetrics(object? state)
        {
            if (!_isMonitoring)
                return;

            try
            {
                var stats = GetCurrentStats();

                // 检查性能阈值
                CheckThresholds(stats);

                // 添加到历史记录
                lock (_historyLock)
                {
                    _history.AddLast(stats);

                    // 限制历史记录数量
                    while (_history.Count > _options.MaxHistoryCount)
                    {
                        _history.RemoveFirst();
                    }
                }

                _logger.LogDebug($"Performance stats collected: Memory={stats.MemoryUsageMB}MB, CPU={stats.CpuUsagePercent:F1}%");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during performance metrics collection");
            }
        }

        private void CheckThresholds(PerformanceStats stats)
        {
            if (stats.CpuUsagePercent > _options.CpuHighThreshold)
            {
                _logger.LogWarning($"High CPU usage detected: {stats.CpuUsagePercent:F1}% > {_options.CpuHighThreshold}%");
            }

            if (stats.MemoryUsageMB > _options.MemoryHighThresholdMB)
            {
                _logger.LogWarning($"High memory usage detected: {stats.MemoryUsageMB}MB > {_options.MemoryHighThresholdMB}MB");
            }
        }

        #region IDisposable

        private bool _disposed = false;

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                StopMonitoring();
                _monitorTimer?.Dispose();
                _cpuCounter?.Dispose();
                _currentProcess?.Dispose();
                _disposed = true;
            }
        }

        #endregion
    }
}