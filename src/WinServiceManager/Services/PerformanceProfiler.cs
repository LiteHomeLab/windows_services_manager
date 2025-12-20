using System;
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace WinServiceManager.Services
{
    /// <summary>
    /// 性能分析器，用于测量操作执行时间
    /// </summary>
    public class PerformanceProfiler : IDisposable
    {
        private readonly string _operationName;
        private readonly ILogger? _logger;
        private readonly Stopwatch _stopwatch;
        private readonly long _startMemory;
        private bool _disposed = false;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="operationName">操作名称</param>
        /// <param name="logger">日志记录器</param>
        public PerformanceProfiler(string operationName, ILogger? logger = null)
        {
            _operationName = operationName ?? throw new ArgumentNullException(nameof(operationName));
            _logger = logger;
            _stopwatch = Stopwatch.StartNew();
            _startMemory = GC.GetTotalMemory(false);

            _logger?.LogDebug("开始性能分析: {OperationName}", _operationName);
        }

        /// <summary>
        /// 开始性能分析
        /// </summary>
        /// <param name="operationName">操作名称</param>
        /// <param name="logger">日志记录器</param>
        /// <returns>性能分析器实例</returns>
        public static PerformanceProfiler StartTimer(string operationName, ILogger? logger = null)
        {
            return new PerformanceProfiler(operationName, logger);
        }

        /// <summary>
        /// 获取已用时间（毫秒）
        /// </summary>
        public long ElapsedMilliseconds => _stopwatch.ElapsedMilliseconds;

        /// <summary>
        /// 获取已用时间
        /// </summary>
        public TimeSpan Elapsed => _stopwatch.Elapsed;

        /// <summary>
        /// 获取内存变化（字节）
        /// </summary>
        public long MemoryDelta => GC.GetTotalMemory(false) - _startMemory;

        public void Dispose()
        {
            if (!_disposed)
            {
                _stopwatch.Stop();
                _disposed = true;

                _logger?.LogDebug(
                    "性能分析完成: {OperationName}, 耗时: {ElapsedMs}ms, 内存变化: {MemoryDelta}bytes",
                    _operationName,
                    ElapsedMilliseconds,
                    MemoryDelta);

                // 如果操作耗时超过1秒，记录警告
                if (ElapsedMilliseconds > 1000)
                {
                    _logger?.LogWarning(
                        "操作耗时较长: {OperationName} 耗时 {ElapsedMs}ms",
                        _operationName,
                        ElapsedMilliseconds);
                }
            }
        }
    }
}