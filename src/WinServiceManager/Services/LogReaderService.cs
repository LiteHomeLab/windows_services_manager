using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WinServiceManager.Models;

namespace WinServiceManager.Services
{
    /// <summary>
    /// 日志读取服务 - 性能优化版本
    /// </summary>
    public class LogReaderService : IDisposable
    {
        private readonly ConcurrentDictionary<string, FileSystemWatcher> _fileWatchers = new();
        private readonly ConcurrentDictionary<string, List<Action<string>>> _subscriptions = new();
        private readonly ConcurrentDictionary<string, long> _lastReadPositions = new();
        private readonly SemaphoreSlim _readSemaphore = new(1, 1);
        private readonly Timer _cleanupTimer;
        private readonly ILogger<LogReaderService>? _logger;
        private readonly ConcurrentQueue<(DateTime Time, string Operation)> _performanceMetrics = new();
        private bool _disposed = false;
        private const int MAX_METRICS_ENTRIES = 1000;
        private const int CLEANUP_INTERVAL_MS = 30000; // 30 seconds

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="logger">日志记录器</param>
        public LogReaderService(ILogger<LogReaderService>? logger = null)
        {
            _logger = logger;

            // 初始化清理定时器
            _cleanupTimer = new Timer(PerformCleanup, null, CLEANUP_INTERVAL_MS, CLEANUP_INTERVAL_MS);

            _logger?.LogDebug("LogReaderService initialized with performance monitoring");
        }

        /// <summary>
        /// 记录性能指标
        /// </summary>
        private void RecordPerformanceMetric(string operation)
        {
            var metric = (DateTime.Now, operation);
            _performanceMetrics.Enqueue(metric);

            // 限制队列大小
            while (_performanceMetrics.Count > MAX_METRICS_ENTRIES)
            {
                _performanceMetrics.TryDequeue(out _);
            }
        }

        /// <summary>
        /// 定期清理资源
        /// </summary>
        private void PerformCleanup(object? state)
        {
            if (_disposed)
                return;

            try
            {
                // 清理无效的文件监视器
                var invalidWatchers = _fileWatchers
                    .Where(kvp => !_subscriptions.ContainsKey(kvp.Key))
                    .ToList();

                foreach (var invalid in invalidWatchers)
                {
                    if (_fileWatchers.TryRemove(invalid.Key, out var watcher))
                    {
                        watcher?.Dispose();
                        _logger?.LogDebug($"Removed unused watcher for: {invalid.Key}");
                    }
                }

                // 清理过期的性能指标
                var cutoff = DateTime.Now.AddMinutes(-5);
                var tempQueue = new Queue<(DateTime, string)>();

                while (_performanceMetrics.TryDequeue(out var metric))
                {
                    if (metric.Time > cutoff)
                    {
                        tempQueue.Enqueue(metric);
                    }
                }

                // 将有效指标放回队列
                while (tempQueue.Count > 0)
                {
                    _performanceMetrics.Enqueue(tempQueue.Dequeue());
                }

                _logger?.LogDebug("Performance cleanup completed");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error during performance cleanup");
            }
        }

        /// <summary>
        /// 获取性能统计
        /// </summary>
        public PerformanceStats GetPerformanceStats()
        {
            var stats = new PerformanceStats();
            var recentMetrics = _performanceMetrics
                .Where(m => m.Time > DateTime.Now.AddMinutes(-1))
                .ToList();

            stats.FileWatcherCount = _fileWatchers.Count;
            stats.SubscriptionCount = _subscriptions.Values.Sum(s => s.Count);
            stats.OperationsPerMinute = recentMetrics.Count;
            stats.MemoryUsageMB = Process.GetCurrentProcess().WorkingSet64 / 1024 / 1024;

            return stats;
        }

        /// <summary>
        /// 订阅文件变更
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="onNewLine">新行回调</param>
        public void SubscribeToFileChanges(string filePath, Action<string> onNewLine)
        {
            if (string.IsNullOrEmpty(filePath) || onNewLine == null)
                return;

            var normalizedPath = Path.GetFullPath(filePath);

            // 添加订阅
            _subscriptions.AddOrUpdate(normalizedPath,
                new List<Action<string>> { onNewLine },
                (key, existing) => { existing.Add(onNewLine); return existing; });

            // 创建文件监视器
            if (!_fileWatchers.ContainsKey(normalizedPath))
            {
                CreateFileWatcher(normalizedPath);
            }
        }

        /// <summary>
        /// 取消订阅文件变更
        /// </summary>
        /// <param name="filePath">文件路径</param>
        public void UnsubscribeFromFileChanges(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return;

            var normalizedPath = Path.GetFullPath(filePath);

            // 移除订阅
            _subscriptions.TryRemove(normalizedPath, out _);

            // 清理文件监视器
            if (!_subscriptions.ContainsKey(normalizedPath))
            {
                if (_fileWatchers.TryRemove(normalizedPath, out var watcher))
                {
                    watcher?.Dispose();
                }
                _lastReadPositions.TryRemove(normalizedPath, out _);
            }
        }

        /// <summary>
        /// 监控新行
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns></returns>
        public async Task MonitorNewLinesAsync(string filePath, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(filePath))
                return;

            var normalizedPath = Path.GetFullPath(filePath);

            while (!cancellationToken.IsCancellationRequested && !_disposed)
            {
                try
                {
                    // 读取新行
                    var newLines = await ReadNewLinesAsync(normalizedPath);
                    if (newLines != null && newLines.Count > 0)
                    {
                        // 通知订阅者
                        if (_subscriptions.TryGetValue(normalizedPath, out var actions))
                        {
                            foreach (var line in newLines)
                            {
                                foreach (var action in actions)
                                {
                                    try
                                    {
                                        action(line);
                                    }
                                    catch (Exception ex)
                                    {
                                        // 忽略回调异常，避免影响其他订阅者
                                        System.Diagnostics.Debug.WriteLine($"Callback error: {ex.Message}");
                                    }
                                }
                            }
                        }
                    }

                    // 等待一段时间再检查
                    await Task.Delay(500, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Monitor error: {ex.Message}");
                    await Task.Delay(1000, cancellationToken); // 出错后等待更长时间
                }
            }
        }

        /// <summary>
        /// 读取最后的 N 行
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="maxLines">最大行数</param>
        /// <returns>日志行数组</returns>
        public async Task<string[]> ReadLastLinesAsync(string filePath, int maxLines)
        {
            if (string.IsNullOrEmpty(filePath))
                return Array.Empty<string>();

            if (!File.Exists(filePath))
                return Array.Empty<string>();

            await _readSemaphore.WaitAsync();
            try
            {
                var lines = new List<string>();

                // 使用 FileStream 读取文件的最后部分
                using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var sr = new StreamReader(fs);

                // 快速定位到文件末尾附近
                fs.Seek(0, SeekOrigin.End);
                long position = fs.Position;

                // 向后查找行分隔符
                int lineCount = 0;
                while (position > 0 && lineCount < maxLines)
                {
                    position--;
                    fs.Position = position;

                    char ch = (char)fs.ReadByte();
                    if (ch == '\n')
                    {
                        lineCount++;
                    }
                }

                // 读取所有行
                fs.Position = position > 0 ? position + 1 : 0;
                var content = await sr.ReadToEndAsync();
                var allLines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

                // 返回最后 N 行
                return allLines.TakeLast(maxLines).ToArray();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ReadLastLines error: {ex.Message}");
                return Array.Empty<string>();
            }
            finally
            {
                _readSemaphore.Release();
            }
        }

        /// <summary>
        /// 获取日志文件大小
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns>文件大小（字节）</returns>
        public long GetLogFileSize(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return 0;

            try
            {
                if (File.Exists(filePath))
                {
                    var fileInfo = new FileInfo(filePath);
                    return fileInfo.Length;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetFileSize error: {ex.Message}");
            }

            return 0;
        }

        /// <summary>
        /// 清空日志文件
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns>是否成功</returns>
        public async Task<bool> ClearLogFileAsync(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return false;

            await _readSemaphore.WaitAsync();
            try
            {
                if (File.Exists(filePath))
                {
                    // 清空文件内容
                    await File.WriteAllTextAsync(filePath, string.Empty);

                    // 重置读取位置
                    var normalizedPath = Path.GetFullPath(filePath);
                    _lastReadPositions.AddOrUpdate(normalizedPath, 0, (key, old) => 0);

                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ClearLog error: {ex.Message}");
                return false;
            }
            finally
            {
                _readSemaphore.Release();
            }
        }

        /// <summary>
        /// 读取新行
        /// </summary>
        private async Task<List<string>> ReadNewLinesAsync(string filePath)
        {
            var newLines = new List<string>();

            try
            {
                if (!File.Exists(filePath))
                    return newLines;

                using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var sr = new StreamReader(fs);

                // 获取文件大小
                long currentPosition = _lastReadPositions.GetOrAdd(filePath, 0);
                if (currentPosition > fs.Length)
                {
                    // 文件可能被重新创建，重置位置
                    currentPosition = 0;
                }

                // 如果有新内容
                if (currentPosition < fs.Length)
                {
                    fs.Position = currentPosition;
                    var content = await sr.ReadToEndAsync();

                    // 分割成行
                    var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    newLines.AddRange(lines);

                    // 更新读取位置
                    _lastReadPositions.AddOrUpdate(filePath, fs.Length, (key, old) => fs.Length);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ReadNewLines error: {ex.Message}");
            }

            return newLines;
        }

        /// <summary>
        /// 创建文件监视器
        /// </summary>
        private void CreateFileWatcher(string filePath)
        {
            try
            {
                var directory = Path.GetDirectoryName(filePath);
                var fileName = Path.GetFileName(filePath);

                if (string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(fileName))
                    return;

                var watcher = new FileSystemWatcher(directory, fileName)
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                    EnableRaisingEvents = true
                };

                watcher.Changed += (sender, e) =>
                {
                    // 延迟一下，确保文件写入完成
                    Task.Delay(100).ContinueWith(async _ =>
                    {
                        var newLines = await ReadNewLinesAsync(filePath);
                        if (_subscriptions.TryGetValue(filePath, out var actions))
                        {
                            foreach (var line in newLines)
                            {
                                foreach (var action in actions)
                                {
                                    try
                                    {
                                        action(line);
                                    }
                                    catch
                                    {
                                        // 忽略回调异常
                                    }
                                }
                            }
                        }
                    });
                };

                _fileWatchers.TryAdd(filePath, watcher);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CreateFileWatcher error: {ex.Message}");
            }
        }

        /// <summary>
        /// 读取日志（保持向后兼容）
        /// </summary>
        public async Task<List<LogEntry>> ReadLogsAsync(string logPath, int maxLines = 1000)
        {
            var logs = new List<LogEntry>();

            try
            {
                var lines = await ReadLastLinesAsync(logPath, maxLines);

                foreach (var line in lines)
                {
                    var entry = ParseLogLine(line);
                    if (entry != null)
                    {
                        logs.Add(entry);
                    }
                }
            }
            catch (Exception ex)
            {
                // 记录错误日志
                logs.Add(new LogEntry
                {
                    Timestamp = DateTime.Now,
                    Level = Models.LogLevel.Error,
                    Message = $"读取日志失败: {ex.Message}",
                    ProcessId = 0,
                    RawLine = string.Empty
                });
            }

            return logs;
        }

        /// <summary>
        /// 解析日志行
        /// </summary>
        private LogEntry? ParseLogLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return null;

            // 简单的日志解析逻辑
            return new LogEntry
            {
                Timestamp = DateTime.Now,
                Level = Models.LogLevel.Info,
                Message = line,
                ProcessId = 0,
                RawLine = line
            };
        }

        #region IDisposable

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                // 清理文件监视器
                foreach (var watcher in _fileWatchers.Values)
                {
                    watcher?.Dispose();
                }
                _fileWatchers.Clear();

                // 清理订阅
                _subscriptions.Clear();

                // 清理信号量
                _readSemaphore?.Dispose();

                _disposed = true;
            }
        }

        #endregion
    }
}