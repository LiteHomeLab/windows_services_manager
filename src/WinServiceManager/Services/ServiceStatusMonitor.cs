using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.Extensions.Logging;
using WinServiceManager.Models;

namespace WinServiceManager.Services
{
    public class ServiceStatusMonitor : IDisposable
    {
        private readonly ServiceManagerService _serviceManager;
        private readonly ILogger<ServiceStatusMonitor> _logger;
        private readonly IPerformanceMonitorService? _performanceMonitor;
        private System.Threading.Timer? _timer;
        private readonly List<Action<List<ServiceItem>>> _subscribers = new();
        private readonly object _lockObject = new object();
        private bool _disposed = false;

        public ServiceStatusMonitor(
            ServiceManagerService serviceManager,
            ILogger<ServiceStatusMonitor> logger,
            IPerformanceMonitorService? performanceMonitor = null)
        {
            _serviceManager = serviceManager ?? throw new ArgumentNullException(nameof(serviceManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _performanceMonitor = performanceMonitor;
        }

        public void StartMonitoring(int intervalSeconds = 10)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ServiceStatusMonitor));

            if (intervalSeconds <= 0)
                throw new ArgumentException("Interval must be greater than 0", nameof(intervalSeconds));

            StopMonitoring();

            _logger.LogInformation("Starting service status monitoring with interval: {IntervalSeconds} seconds", intervalSeconds);

            // 使用TaskCreationOptions.LongRunning避免长时间运行的任务占用线程池线程
            _timer = new Timer(async _ => await RefreshStatusAsync(),
                null,
                TimeSpan.Zero,
                TimeSpan.FromSeconds(intervalSeconds));
        }

        public void StopMonitoring()
        {
            if (_timer != null)
            {
                _logger.LogInformation("Stopping service status monitoring");

                var timer = _timer;
                _timer = null;

                // Dispose outside of lock to avoid deadlock
                timer?.Dispose();
            }
        }

        public void Subscribe(Action<List<ServiceItem>> callback)
        {
            if (callback == null)
                throw new ArgumentNullException(nameof(callback));

            if (_disposed)
                throw new ObjectDisposedException(nameof(ServiceStatusMonitor));

            lock (_lockObject)
            {
                if (!_subscribers.Contains(callback))
                {
                    _subscribers.Add(callback);
                    _logger.LogDebug("Added new subscriber. Total subscribers: {Count}", _subscribers.Count);
                }
            }
        }

        public void Unsubscribe(Action<List<ServiceItem>> callback)
        {
            if (callback == null)
                throw new ArgumentNullException(nameof(callback));

            lock (_lockObject)
            {
                if (_subscribers.Remove(callback))
                {
                    _logger.LogDebug("Removed subscriber. Total subscribers: {Count}", _subscribers.Count);
                }
            }
        }

        private async Task RefreshStatusAsync()
        {
            if (_disposed)
                return;

            // 启用性能监控
            using var _ = PerformanceProfiler.StartTimer("ServiceStatusMonitor.RefreshStatus", _logger);

            try
            {
                _logger.LogDebug("Refreshing service status");

                // 使用ConfigureAwait(false)避免上下文切换
                var services = await _serviceManager.GetAllServicesAsync().ConfigureAwait(false);

                // 使用本地副本减少锁持有时间
                Action<List<ServiceItem>>[] subscribersCopy;
                lock (_lockObject)
                {
                    if (_subscribers.Count == 0)
                        return;

                    subscribersCopy = _subscribers.ToArray();
                }

                // 并行通知所有订阅者以提高性能
                if (subscribersCopy.Length > 0)
                {
                    await Task.Run(() =>
                    {
                        Parallel.ForEach(subscribersCopy, subscriber =>
                        {
                            try
                            {
                                subscriber(services);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Subscriber callback failed during status refresh");
                                // Continue with other subscribers even if one fails
                            }
                        });
                    }).ConfigureAwait(false);
                }

                _logger.LogDebug("Service status refresh completed. Notified {SubscriberCount} subscribers",
                    subscribersCopy.Length);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to refresh service status");
                // Don't re-throw to prevent timer from stopping
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _logger.LogInformation("Disposing ServiceStatusMonitor");

            StopMonitoring();

            lock (_lockObject)
            {
                _subscribers.Clear();
            }

            _disposed = true;
        }
    }
}