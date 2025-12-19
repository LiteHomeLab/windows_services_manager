# 服务管理模块设计

## 模块概述

服务管理模块是 WinServiceManager 的核心功能模块，负责服务的创建、安装、启动、停止、卸载等生命周期管理。该模块通过封装 WinSW 命令行工具，提供服务管理的抽象层。

## 1. WinSWWrapper - WinSW 封装类

### 类定义
```csharp
// File: Services/WinSWWrapper.cs
using System.Diagnostics;
using WinServiceManager.Models;

namespace WinServiceManager.Services
{
    /// <summary>
    /// WinSW 命令行工具的封装类
    /// </summary>
    public class WinSWWrapper
    {
        private readonly ILogger<WinSWWrapper> _logger;

        public WinSWWrapper(ILogger<WinSWWrapper> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// 安装服务
        /// </summary>
        /// <param name="service">服务信息</param>
        /// <returns>操作结果</returns>
        public async Task<ServiceOperationResult> InstallAsync(ServiceItem service)
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                _logger.LogInformation($"Installing service: {service.DisplayName} ({service.Id})");

                // 确保服务目录存在
                EnsureServiceDirectoryExists(service);

                // 复制 WinSW 可执行文件
                await CopyWinSWExecutableAsync(service);

                // 生成配置文件
                await GenerateConfigFileAsync(service);

                // 执行安装命令
                var result = await ExecuteWinSWCommandAsync(
                    service.WinSWExecutablePath,
                    "install",
                    TimeSpan.FromMinutes(2)
                );

                stopwatch.Stop();

                if (result.ExitCode == 0)
                {
                    _logger.LogInformation($"Service {service.DisplayName} installed successfully");
                    return ServiceOperationResult.SuccessResult(
                        ServiceOperationType.Install,
                        stopwatch.ElapsedMilliseconds
                    );
                }
                else
                {
                    var error = $"Failed to install service {service.DisplayName}";
                    var details = $"Exit code: {result.ExitCode}, Output: {result.StandardOutput}";
                    _logger.LogError($"{error}. {details}");

                    return ServiceOperationResult.FailureResult(
                        ServiceOperationType.Install,
                        error,
                        details,
                        stopwatch.ElapsedMilliseconds
                    );
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, $"Error installing service {service.DisplayName}");

                return ServiceOperationResult.FailureResult(
                    ServiceOperationType.Install,
                    ex.Message,
                    ex.ToString(),
                    stopwatch.ElapsedMilliseconds
                );
            }
        }

        /// <summary>
        /// 卸载服务
        /// </summary>
        public async Task<ServiceOperationResult> UninstallAsync(ServiceItem service)
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                _logger.LogInformation($"Uninstalling service: {service.DisplayName}");

                // 先停止服务
                await StopServiceAsync(service.Id);

                // 执行卸载命令
                var result = await ExecuteWinSWCommandAsync(
                    service.WinSWExecutablePath,
                    "uninstall",
                    TimeSpan.FromMinutes(1)
                );

                // 删除服务目录
                if (Directory.Exists(service.ServiceDirectory))
                {
                    await Task.Run(() =>
                    {
                        // 等待文件释放
                        Thread.Sleep(1000);
                        Directory.Delete(service.ServiceDirectory, true);
                    });
                }

                stopwatch.Stop();

                if (result.ExitCode == 0)
                {
                    _logger.LogInformation($"Service {service.DisplayName} uninstalled successfully");
                    return ServiceOperationResult.SuccessResult(
                        ServiceOperationType.Uninstall,
                        stopwatch.ElapsedMilliseconds
                    );
                }
                else
                {
                    var error = $"Failed to uninstall service {service.DisplayName}";
                    var details = $"Exit code: {result.ExitCode}, Output: {result.StandardOutput}";
                    return ServiceOperationResult.FailureResult(
                        ServiceOperationType.Uninstall,
                        error,
                        details,
                        stopwatch.ElapsedMilliseconds
                    );
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, $"Error uninstalling service {service.DisplayName}");

                return ServiceOperationResult.FailureResult(
                    ServiceOperationType.Uninstall,
                    ex.Message,
                    ex.ToString(),
                    stopwatch.ElapsedMilliseconds
                );
            }
        }

        /// <summary>
        /// 启动服务
        /// </summary>
        public async Task<ServiceOperationResult> StartAsync(string serviceName)
        {
            return await ExecuteServiceControlAsync(serviceName, "start");
        }

        /// <summary>
        /// 停止服务
        /// </summary>
        public async Task<ServiceOperationResult> StopAsync(string serviceName)
        {
            return await ExecuteServiceControlAsync(serviceName, "stop");
        }

        /// <summary>
        /// 重启服务
        /// </summary>
        public async Task<ServiceOperationResult> RestartAsync(string serviceName)
        {
            return await ExecuteServiceControlAsync(serviceName, "restart");
        }

        /// <summary>
        /// 获取服务状态
        /// </summary>
        public ServiceStatus GetServiceStatus(string serviceName)
        {
            try
            {
                using var controller = new ServiceController(serviceName);
                return controller.Status switch
                {
                    ServiceControllerStatus.Running => ServiceStatus.Running,
                    ServiceControllerStatus.Stopped => ServiceStatus.Stopped,
                    ServiceControllerStatus.Paused => ServiceStatus.Paused,
                    ServiceControllerStatus.StartPending => ServiceStatus.Starting,
                    ServiceControllerStatus.StopPending => ServiceStatus.Stopping,
                    ServiceControllerStatus.PausePending => ServiceStatus.Starting,
                    ServiceControllerStatus.ContinuePending => ServiceStatus.Starting,
                    _ => ServiceStatus.Error
                };
            }
            catch (InvalidOperationException)
            {
                // 服务不存在
                return ServiceStatus.NotInstalled;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting status for service {serviceName}");
                return ServiceStatus.Error;
            }
        }

        #region Private Methods

        private void EnsureServiceDirectoryExists(ServiceItem service)
        {
            var directory = service.ServiceDirectory;
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
                _logger.LogDebug($"Created service directory: {directory}");
            }

            // 创建日志目录
            var logDirectory = service.LogDirectory;
            if (!Directory.Exists(logDirectory))
            {
                Directory.CreateDirectory(logDirectory);
                _logger.LogDebug($"Created log directory: {logDirectory}");
            }
        }

        private async Task CopyWinSWExecutableAsync(ServiceItem service)
        {
            var sourcePath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "templates",
                "WinSW-x64.exe"
            );

            if (!File.Exists(sourcePath))
            {
                throw new FileNotFoundException($"WinSW executable not found at {sourcePath}");
            }

            File.Copy(sourcePath, service.WinSWExecutablePath, true);
            _logger.LogDebug($"Copied WinSW executable to {service.WinSWExecutablePath}");
        }

        private async Task GenerateConfigFileAsync(ServiceItem service)
        {
            var config = service.GenerateWinSWConfig();
            await File.WriteAllTextAsync(service.WinSWConfigPath, config);
            _logger.LogDebug($"Generated config file at {service.WinSWConfigPath}");
        }

        private async Task<CommandResult> ExecuteWinSWCommandAsync(
            string executablePath,
            string command,
            TimeSpan timeout)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                Arguments = command,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = startInfo };

            var outputBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();

            process.OutputDataReceived += (s, e) =>
            {
                if (e.Data != null) outputBuilder.AppendLine(e.Data);
            };
            process.ErrorDataReceived += (s, e) =>
            {
                if (e.Data != null) errorBuilder.AppendLine(e.Data);
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            var exited = await process.WaitForExitAsync((int)timeout.TotalMilliseconds);

            if (!exited)
            {
                process.Kill();
                throw new TimeoutException($"Command '{command}' timed out after {timeout.TotalSeconds} seconds");
            }

            return new CommandResult
            {
                ExitCode = process.ExitCode,
                StandardOutput = outputBuilder.ToString(),
                StandardError = errorBuilder.ToString()
            };
        }

        private async Task<ServiceOperationResult> ExecuteServiceControlAsync(
            string serviceName,
            string command)
        {
            var stopwatch = Stopwatch.StartNew();
            var operationType = command switch
            {
                "start" => ServiceOperationType.Start,
                "stop" => ServiceOperationType.Stop,
                "restart" => ServiceOperationType.Restart,
                _ => ServiceOperationType.QueryStatus
            };

            try
            {
                _logger.LogInformation($"{command} service: {serviceName}");

                using var controller = new ServiceController(serviceName);

                switch (command)
                {
                    case "start":
                        if (controller.Status == ServiceControllerStatus.Running)
                            return ServiceOperationResult.SuccessResult(operationType, stopwatch.ElapsedMilliseconds);

                        await Task.Run(() => controller.Start());
                        break;

                    case "stop":
                        if (controller.Status == ServiceControllerStatus.Stopped)
                            return ServiceOperationResult.SuccessResult(operationType, stopwatch.ElapsedMilliseconds);

                        await Task.Run(() => controller.Stop());
                        break;

                    case "restart":
                        await Task.Run(() =>
                        {
                            if (controller.Status != ServiceControllerStatus.Stopped)
                                controller.Stop();
                            controller.Start();
                        });
                        break;
                }

                // 等待状态变更
                await WaitForStatusChange(controller, command);

                stopwatch.Stop();

                _logger.LogInformation($"Service {serviceName} {command} completed successfully");
                return ServiceOperationResult.SuccessResult(operationType, stopwatch.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, $"Error {command} service {serviceName}");

                return ServiceOperationResult.FailureResult(
                    operationType,
                    ex.Message,
                    ex.ToString(),
                    stopwatch.ElapsedMilliseconds
                );
            }
        }

        private async Task WaitForStatusChange(
            ServiceController controller,
            string command)
        {
            var targetStatus = command switch
            {
                "start" => ServiceControllerStatus.Running,
                "stop" => ServiceControllerStatus.Stopped,
                _ => controller.Status
            };

            var timeout = TimeSpan.FromSeconds(30);
            var startTime = DateTime.Now;

            while (DateTime.Now - startTime < timeout)
            {
                controller.Refresh();
                if (controller.Status == targetStatus)
                    break;

                await Task.Delay(500);
            }

            controller.Refresh();
        }

        private async Task StopServiceAsync(string serviceName)
        {
            try
            {
                using var controller = new ServiceController(serviceName);
                if (controller.Status == ServiceControllerStatus.Running)
                {
                    await Task.Run(() => controller.Stop());
                    await WaitForStatusChange(controller, "stop");
                }
            }
            catch
            {
                // 服务可能已经停止或不存在，忽略错误
            }
        }

        #endregion
    }

    /// <summary>
    /// 命令执行结果
    /// </summary>
    internal class CommandResult
    {
        public int ExitCode { get; set; }
        public string StandardOutput { get; set; } = string.Empty;
        public string StandardError { get; set; } = string.Empty;
    }
}
```

## 2. ServiceManagerService - 服务管理核心服务

### 类定义
```csharp
// File: Services/ServiceManagerService.cs
using System.Collections.ObjectModel;
using WinServiceManager.Models;

namespace WinServiceManager.Services
{
    /// <summary>
    /// 服务管理核心服务
    /// </summary>
    public class ServiceManagerService
    {
        private readonly WinSWWrapper _winSWWrapper;
        private readonly DataService _dataService;
        private readonly ILogger<ServiceManagerService> _logger;
        private readonly Timer _refreshTimer;

        private readonly ObservableCollection<ServiceItem> _services;

        public ServiceManagerService(
            WinSWWrapper winSWWrapper,
            DataService dataService,
            ILogger<ServiceManagerService> logger)
        {
            _winSWWrapper = winSWWrapper;
            _dataService = dataService;
            _logger = logger;
            _services = new ObservableCollection<ServiceItem>();

            // 初始化定时刷新
            _refreshTimer = new Timer(RefreshServiceStatuses, null, Timeout.Infinite, Timeout.Infinite);
        }

        /// <summary>
        /// 服务集合（只读）
        /// </summary>
        public IReadOnlyObservableCollection<ServiceItem> Services => _services;

        /// <summary>
        /// 初始化服务管理器
        /// </summary>
        public async Task InitializeAsync()
        {
            try
            {
                _logger.LogInformation("Initializing ServiceManager");

                // 加载已保存的服务
                var savedServices = await _dataService.LoadServicesAsync();
                _services.Clear();

                foreach (var service in savedServices)
                {
                    _services.Add(service);
                }

                // 刷新服务状态
                await RefreshAllServicesStatusAsync();

                // 启动定时刷新（30秒间隔）
                _refreshTimer.Change(TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));

                _logger.LogInformation($"ServiceManager initialized with {_services.Count} services");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing ServiceManager");
                throw;
            }
        }

        /// <summary>
        /// 创建并安装新服务
        /// </summary>
        public async Task<ServiceOperationResult> CreateServiceAsync(ServiceCreateRequest request)
        {
            var service = new ServiceItem
            {
                DisplayName = request.DisplayName,
                Description = request.Description ?? "Managed by WinServiceManager",
                ExecutablePath = request.ExecutablePath,
                ScriptPath = request.ScriptPath,
                Arguments = request.Arguments,
                WorkingDirectory = request.WorkingDirectory,
                Status = ServiceStatus.Installing
            };

            try
            {
                _logger.LogInformation($"Creating new service: {service.DisplayName}");

                // 添加到集合
                _services.Add(service);

                // 安装服务
                var installResult = await _winSWWrapper.InstallAsync(service);

                if (!installResult.Success)
                {
                    // 安装失败，从集合中移除
                    _services.Remove(service);
                    return installResult;
                }

                // 保存到数据文件
                await _dataService.SaveServicesAsync(_services.ToList());

                // 自动启动（如果请求）
                if (request.AutoStart)
                {
                    var startResult = await _winSWWrapper.StartAsync(service.Id);
                    if (startResult.Success)
                    {
                        service.Status = _winSWWrapper.GetServiceStatus(service.Id);
                    }
                }
                else
                {
                    service.Status = ServiceStatus.Stopped;
                }

                service.UpdatedAt = DateTime.Now;

                // 再次保存（更新状态）
                await _dataService.SaveServicesAsync(_services.ToList());

                _logger.LogInformation($"Service {service.DisplayName} created successfully");
                return installResult;
            }
            catch (Exception ex)
            {
                // 清理失败的服务
                if (_services.Contains(service))
                {
                    _services.Remove(service);
                }

                // 删除可能已创建的文件
                await CleanupFailedServiceAsync(service);

                _logger.LogError(ex, $"Error creating service {service.DisplayName}");

                return ServiceOperationResult.FailureResult(
                    ServiceOperationType.Install,
                    ex.Message,
                    ex.ToString()
                );
            }
        }

        /// <summary>
        /// 启动服务
        /// </summary>
        public async Task<ServiceOperationResult> StartServiceAsync(string serviceId)
        {
            var service = _services.FirstOrDefault(s => s.Id == serviceId);
            if (service == null)
            {
                return ServiceOperationResult.FailureResult(
                    ServiceOperationType.Start,
                    "Service not found"
                );
            }

            if (!service.Status.CanStart())
            {
                return ServiceOperationResult.FailureResult(
                    ServiceOperationType.Start,
                    $"Service cannot be started in current status: {service.Status.GetDisplayText()}"
                );
            }

            service.Status = ServiceStatus.Starting;

            var result = await _winSWWrapper.StartAsync(serviceId);

            if (result.Success)
            {
                service.Status = ServiceStatus.Running;
                service.UpdatedAt = DateTime.Now;
                await _dataService.SaveServicesAsync(_services.ToList());
            }
            else
            {
                service.Status = ServiceStatus.Error;
            }

            return result;
        }

        /// <summary>
        /// 停止服务
        /// </summary>
        public async Task<ServiceOperationResult> StopServiceAsync(string serviceId)
        {
            var service = _services.FirstOrDefault(s => s.Id == serviceId);
            if (service == null)
            {
                return ServiceOperationResult.FailureResult(
                    ServiceOperationType.Stop,
                    "Service not found"
                );
            }

            if (!service.Status.CanStop())
            {
                return ServiceOperationResult.FailureResult(
                    ServiceOperationType.Stop,
                    $"Service cannot be stopped in current status: {service.Status.GetDisplayText()}"
                );
            }

            service.Status = ServiceStatus.Stopping;

            var result = await _winSWWrapper.StopAsync(serviceId);

            if (result.Success)
            {
                service.Status = ServiceStatus.Stopped;
                service.UpdatedAt = DateTime.Now;
                await _dataService.SaveServicesAsync(_services.ToList());
            }
            else
            {
                service.Status = ServiceStatus.Error;
            }

            return result;
        }

        /// <summary>
        /// 重启服务
        /// </summary>
        public async Task<ServiceOperationResult> RestartServiceAsync(string serviceId)
        {
            var service = _services.FirstOrDefault(s => s.Id == serviceId);
            if (service == null)
            {
                return ServiceOperationResult.FailureResult(
                    ServiceOperationType.Restart,
                    "Service not found"
                );
            }

            var stopResult = await StopServiceAsync(serviceId);
            if (!stopResult.Success)
            {
                return stopResult;
            }

            await Task.Delay(1000); // 等待1秒

            return await StartServiceAsync(serviceId);
        }

        /// <summary>
        /// 卸载服务
        /// </summary>
        public async Task<ServiceOperationResult> UninstallServiceAsync(string serviceId)
        {
            var service = _services.FirstOrDefault(s => s.Id == serviceId);
            if (service == null)
            {
                return ServiceOperationResult.FailureResult(
                    ServiceOperationType.Uninstall,
                    "Service not found"
                );
            }

            if (!service.Status.CanUninstall())
            {
                return ServiceOperationResult.FailureResult(
                    ServiceOperationType.Uninstall,
                    $"Service cannot be uninstalled in current status: {service.Status.GetDisplayText()}"
                );
            }

            service.Status = ServiceStatus.Uninstalling;

            var result = await _winSWWrapper.UninstallAsync(service);

            if (result.Success)
            {
                _services.Remove(service);
                await _dataService.SaveServicesAsync(_services.ToList());
            }
            else
            {
                service.Status = ServiceStatus.Error;
            }

            return result;
        }

        /// <summary>
        /// 刷新所有服务的状态
        /// </summary>
        public async Task RefreshAllServicesStatusAsync()
        {
            foreach (var service in _services)
            {
                try
                {
                    var newStatus = _winSWWrapper.GetServiceStatus(service.Id);
                    if (service.Status != newStatus)
                    {
                        service.Status = newStatus;
                        service.UpdatedAt = DateTime.Now;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error refreshing status for service {service.Id}");
                    service.Status = ServiceStatus.Error;
                }
            }

            await _dataService.SaveServicesAsync(_services.ToList());
        }

        /// <summary>
        /// 手动刷新服务状态
        /// </summary>
        public async Task<ServiceStatus> RefreshServiceStatusAsync(string serviceId)
        {
            var service = _services.FirstOrDefault(s => s.Id == serviceId);
            if (service == null)
            {
                return ServiceStatus.NotInstalled;
            }

            try
            {
                var newStatus = _winSWWrapper.GetServiceStatus(serviceId);
                if (service.Status != newStatus)
                {
                    service.Status = newStatus;
                    service.UpdatedAt = DateTime.Now;
                    await _dataService.SaveServicesAsync(_services.ToList());
                }
                return newStatus;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error refreshing status for service {serviceId}");
                return ServiceStatus.Error;
            }
        }

        #region Private Methods

        private async void RefreshServiceStatuses(object? state)
        {
            try
            {
                await RefreshAllServicesStatusAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during automatic status refresh");
            }
        }

        private async Task CleanupFailedServiceAsync(ServiceItem service)
        {
            try
            {
                if (Directory.Exists(service.ServiceDirectory))
                {
                    await Task.Run(() =>
                    {
                        // 等待文件释放
                        Thread.Sleep(1000);
                        Directory.Delete(service.ServiceDirectory, true);
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error cleaning up failed service {service.Id}");
            }
        }

        #endregion

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            _refreshTimer?.Dispose();
        }
    }
}
```

## 3. 使用示例

### 在 ViewModel 中使用
```csharp
public class MainWindowViewModel : ObservableObject
{
    private readonly ServiceManagerService _serviceManager;

    public MainWindowViewModel(ServiceManagerService serviceManager)
    {
        _serviceManager = serviceManager;

        // 订阅服务集合变化
        _serviceManager.Services.CollectionChanged += OnServicesCollectionChanged;
    }

    // 创建服务命令
    [RelayCommand]
    private async Task CreateService()
    {
        var request = new ServiceCreateRequest
        {
            DisplayName = "My Service",
            ExecutablePath = @"C:\app.exe",
            WorkingDirectory = @"C:\app",
            AutoStart = true
        };

        var result = await _serviceManager.CreateServiceAsync(request);

        if (!result.Success)
        {
            // 显示错误信息
            await ShowError(result.ErrorMessage!);
        }
    }

    // 启动服务命令
    [RelayCommand]
    private async Task StartService(ServiceItem service)
    {
        var result = await _serviceManager.StartServiceAsync(service.Id);

        if (!result.Success)
        {
            await ShowError(result.ErrorMessage!);
        }
    }
}
```

## 4. 配置和依赖注入

### 在 App.xaml.cs 中配置
```csharp
protected override async void OnStartup(StartupEventArgs e)
{
    base.OnStartup(e);

    var services = new ServiceCollection();

    // 添加服务
    services.AddLogging(builder => builder.AddConsole());
    services.AddSingleton<WinSWWrapper>();
    services.AddSingleton<DataService>();
    services.AddSingleton<ServiceManagerService>();

    var serviceProvider = services.BuildServiceProvider();

    // 初始化服务管理器
    var serviceManager = serviceProvider.GetService<ServiceManagerService>();
    await serviceManager.InitializeAsync();

    // 设置主窗口和数据上下文
    var mainWindow = new MainWindow
    {
        DataContext = new MainWindowViewModel(serviceManager)
    };

    mainWindow.Show();
}
```

## 5. 错误处理策略

1. **命令执行超时**: 设置合理的超时时间，避免进程挂起
2. **服务状态同步**: 定时刷新服务状态，确保 UI 显示最新状态
3. **文件操作错误**: 处理文件被占用、权限不足等情况
4. **清理失败安装**: 安装失败时自动清理已创建的文件和目录
5. **日志记录**: 记录所有操作和错误，便于排查问题

## 6. 性能优化

1. **异步操作**: 所有服务操作都使用异步方法，避免 UI 阻塞
2. **批量状态刷新**: 一次性刷新所有服务状态，减少 Windows API 调用
3. **定时刷新限制**: 设置合理的刷新间隔，避免过度消耗系统资源
4. **日志缓冲**: 日志读取使用缓冲，避免频繁文件 I/O