using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using CommunityToolkit.Mvvm.Input;
using WinServiceManager.Models;
using WinServiceManager.Services;

namespace WinServiceManager.ViewModels
{
    /// <summary>
    /// 状态颜色缓存，避免重复创建 Brush 对象
    /// </summary>
    internal static class StatusColorCache
    {
        public static readonly System.Windows.Media.Brush RunningBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(76, 175, 80));     // Green
        public static readonly System.Windows.Media.Brush StoppedBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(244, 67, 54));      // Red
        public static readonly System.Windows.Media.Brush StartingBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 152, 0));    // Orange
        public static readonly System.Windows.Media.Brush StoppingBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 152, 0));    // Orange
        public static readonly System.Windows.Media.Brush InstallingBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(156, 39, 176));  // Purple
        public static readonly System.Windows.Media.Brush UninstallingBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(156, 39, 176)); // Purple
        public static readonly System.Windows.Media.Brush ErrorBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(183, 28, 28));       // Dark Red
        public static readonly System.Windows.Media.Brush PausedBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 152, 0));     // Orange
        public static readonly System.Windows.Media.Brush NotInstalledBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(158, 158, 158)); // Gray
        public static readonly System.Windows.Media.Brush DefaultBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(158, 158, 158)); // Gray
    }

    /// <summary>
    /// 服务项的视图模型
    /// </summary>
    public partial class ServiceItemViewModel : BaseViewModel, IDisposable
    {
        /// <summary>
        /// 请求编辑服务事件
        /// </summary>
        public event EventHandler<ServiceItem>? EditRequested;

        /// <summary>
        /// 请求删除服务事件
        /// </summary>
        public event EventHandler<ServiceItem>? DeleteRequested;

        private readonly ServiceManagerService _serviceManager;
        private readonly ServicePollingCoordinator? _pollingCoordinator;
        private ServiceItem _service;
        private bool _isBusy;
        private ServiceStatus _cachedStatus;  // 缓存状态以避免绑定问题

        public ServiceItemViewModel(ServiceItem service, ServiceManagerService serviceManager, ServicePollingCoordinator? pollingCoordinator = null)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _serviceManager = serviceManager ?? throw new ArgumentNullException(nameof(serviceManager));
            _pollingCoordinator = pollingCoordinator;
            _cachedStatus = service.Status;  // 初始化缓存状态
        }

        #region Properties

        /// <summary>
        /// 服务数据模型
        /// </summary>
        public ServiceItem Service
        {
            get => _service;
            private set => SetProperty(ref _service, value);
        }

        /// <summary>
        /// 服务显示名称
        /// </summary>
        public string DisplayName => Service.DisplayName;

        /// <summary>
        /// 服务描述
        /// </summary>
        public string Description => Service.Description;

        /// <summary>
        /// 服务状态
        /// </summary>
        public ServiceStatus Status
        {
            get => _cachedStatus;
            private set
            {
                if (_cachedStatus != value)
                {
                    _cachedStatus = value;
                    Service.Status = value;  // 同时更新底层对象
                    // 总是触发通知，确保 UI 更新
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(StatusDisplay));
                    OnPropertyChanged(nameof(StatusColor));
                    OnPropertyChanged(nameof(CanStart));
                    OnPropertyChanged(nameof(CanStop));
                    OnPropertyChanged(nameof(CanRestart));
                    OnPropertyChanged(nameof(CanUninstall));
                    OnPropertyChanged(nameof(CanDelete));
                    OnPropertyChanged(nameof(IsTransitioning));
                }
            }
        }

        /// <summary>
        /// 状态显示文本
        /// </summary>
        public string StatusDisplay => Status.GetDisplayText();

        /// <summary>
        /// 状态对应的颜色
        /// </summary>
        public System.Windows.Media.Brush StatusColor => Status switch
        {
            ServiceStatus.Running => StatusColorCache.RunningBrush,
            ServiceStatus.Stopped => StatusColorCache.StoppedBrush,
            ServiceStatus.Starting => StatusColorCache.StartingBrush,
            ServiceStatus.Stopping => StatusColorCache.StoppingBrush,
            ServiceStatus.Installing => StatusColorCache.InstallingBrush,
            ServiceStatus.Uninstalling => StatusColorCache.UninstallingBrush,
            ServiceStatus.Error => StatusColorCache.ErrorBrush,
            ServiceStatus.Paused => StatusColorCache.PausedBrush,
            ServiceStatus.NotInstalled => StatusColorCache.NotInstalledBrush,
            _ => StatusColorCache.DefaultBrush
        };

        /// <summary>
        /// 是否处于过渡状态
        /// </summary>
        public bool IsTransitioning => Status.IsTransitioning();

        /// <summary>
        /// 是否正在执行操作
        /// </summary>
        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                if (SetProperty(ref _isBusy, value))
                {
                    OnPropertyChanged(nameof(CanStart));
                    OnPropertyChanged(nameof(CanStop));
                    OnPropertyChanged(nameof(CanRestart));
                    OnPropertyChanged(nameof(CanUninstall));
                    OnPropertyChanged(nameof(CanDelete));
                }
            }
        }

        /// <summary>
        /// 创建时间
        /// </summary>
        public string CreatedAt => Service.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss");

        /// <summary>
        /// 可执行文件路径
        /// </summary>
        public string ExecutablePath => Service.ExecutablePath;

        /// <summary>
        /// 工作目录
        /// </summary>
        public string WorkingDirectory => Service.WorkingDirectory;

        /// <summary>
        /// 启动参数
        /// </summary>
        public string Arguments => Service.Arguments;

        /// <summary>
        /// 最后一次启动结果
        /// </summary>
        public StartupResult LastStartupResult
        {
            get => Service.LastStartupResult;
            private set => Service.LastStartupResult = value;
        }

        /// <summary>
        /// 启动结果显示文本
        /// </summary>
        public string StartupResultDisplay => LastStartupResult switch
        {
            StartupResult.Success => "启动成功",
            StartupResult.Failed => "启动失败",
            StartupResult.Warning => "警告",
            _ => "未知"
        };

        /// <summary>
        /// 启动结果对应的颜色
        /// </summary>
        public System.Windows.Media.Brush StartupResultColor => LastStartupResult switch
        {
            StartupResult.Success => StatusColorCache.RunningBrush,      // 绿色
            StartupResult.Failed => StatusColorCache.ErrorBrush,         // 红色
            StartupResult.Warning => StatusColorCache.StartingBrush,     // 黄色
            _ => StatusColorCache.DefaultBrush                           // 灰色
        };

        /// <summary>
        /// 启动参数显示（截断过长的参数）
        /// </summary>
        public string ArgumentsDisplay
        {
            get
            {
                var args = Service.Arguments;
                if (string.IsNullOrEmpty(args))
                    return "-";

                const int maxLength = 50;
                return args.Length > maxLength ? args.Substring(0, maxLength) + "..." : args;
            }
        }

        /// <summary>
        /// 完整启动参数（用于 Tooltip）
        /// </summary>
        public string FullArgumentsTooltip => Service.GetFullArguments();

        /// <summary>
        /// 最后一次启动错误消息
        /// </summary>
        public string? LastStartupErrorMessage => Service.LastStartupErrorMessage;

        #endregion

        #region Command Properties

        /// <summary>
        /// 是否可以启动
        /// </summary>
        public bool CanStart => !IsBusy && Status.CanStart();

        /// <summary>
        /// 是否可以停止
        /// </summary>
        public bool CanStop => !IsBusy && Status.CanStop();

        /// <summary>
        /// 是否可以重启
        /// </summary>
        public bool CanRestart => !IsBusy && Status == ServiceStatus.Running;

        /// <summary>
        /// 是否可以卸载
        /// </summary>
        public bool CanUninstall => !IsBusy && Status.CanUninstall();

        /// <summary>
        /// 是否可以编辑（只有停止的服务才能编辑）
        /// </summary>
        public bool CanEdit => !IsBusy && (Status == ServiceStatus.Stopped || Status == ServiceStatus.NotInstalled);

        /// <summary>
        /// 是否可以删除（不能删除正在运行的服务）
        /// </summary>
        public bool CanDelete => !IsBusy && Status != ServiceStatus.Running;

        /// <summary>
        /// 依赖服务显示文本
        /// </summary>
        public string DependenciesDisplay
        {
            get
            {
                if (!Service.Dependencies.Any())
                    return "无";

                return $"{Service.Dependencies.Count} 个依赖";
            }
        }

        /// <summary>
        /// 依赖服务详细信息
        /// </summary>
        public string DependenciesDetails
        {
            get
            {
                if (!Service.Dependencies.Any())
                    return "无依赖服务";

                return string.Join(", ", Service.Dependencies);
            }
        }

        #endregion

        #region Commands

        [RelayCommand(CanExecute = nameof(CanStart))]
        private async Task StartAsync()
        {
            if (Status == ServiceStatus.NotInstalled)
            {
                // 如果服务未安装，需要先安装
                var installResult = await _serviceManager.InstallServiceAsync(Service);
                if (!installResult.Success)
                {
                    ShowError($"启动服务失败: {installResult.ErrorMessage}");
                    return;
                }
            }

            // 先设置为启动中状态
            Status = ServiceStatus.Starting;

            var startResult = await _serviceManager.StartServiceAsync(Service);

            if (!startResult.Success)
            {
                Status = ServiceStatus.Stopped;
                ShowError($"启动服务失败: {startResult.ErrorMessage}");
                return;
            }

            // 操作成功后通知协调器开始监控此服务
            _pollingCoordinator?.AddPendingService(Service.Id);
        }

        [RelayCommand(CanExecute = nameof(CanStop))]
        private async Task StopAsync()
        {
            // 先设置为停止中状态
            Status = ServiceStatus.Stopping;

            var result = await _serviceManager.StopServiceAsync(Service);

            if (!result.Success)
            {
                Status = ServiceStatus.Running;
                ShowError($"停止服务失败: {result.ErrorMessage}");
                return;
            }

            // 操作成功后通知协调器开始监控此服务
            _pollingCoordinator?.AddPendingService(Service.Id);
        }

        [RelayCommand(CanExecute = nameof(CanRestart))]
        private async Task RestartAsync()
        {
            // 先停止服务
            Status = ServiceStatus.Stopping;
            var stopResult = await _serviceManager.StopServiceAsync(Service);

            if (!stopResult.Success)
            {
                Status = ServiceStatus.Running;
                ShowError($"停止服务失败（重启操作）: {stopResult.ErrorMessage}");
                return;
            }

            // 通知协调器监控停止状态
            _pollingCoordinator?.AddPendingService(Service.Id);

            // 等待一小段时间确保停止操作完成
            await Task.Delay(1000);

            // 启动服务
            Status = ServiceStatus.Starting;
            var startResult = await _serviceManager.StartServiceAsync(Service);

            if (!startResult.Success)
            {
                Status = ServiceStatus.Stopped;
                ShowError($"重启服务失败: {startResult.ErrorMessage}");
                return;
            }

            // 再次通知协调器监控启动状态
            _pollingCoordinator?.AddPendingService(Service.Id);
        }

        [RelayCommand(CanExecute = nameof(CanUninstall))]
        private async Task UninstallAsync()
        {
            // 显示确认对话框
            var result = MessageBox.Show(
                $"确定要卸载服务 '{DisplayName}' 吗？\n\n此操作不可恢复，服务的所有文件将被删除。",
                "确认卸载",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
                return;

            // 先停止服务（如果正在运行）
            if (Status == ServiceStatus.Running)
            {
                Status = ServiceStatus.Stopping;
                var stopResult = await _serviceManager.StopServiceAsync(Service);
                if (!stopResult.Success)
                {
                    Status = ServiceStatus.Running;
                    ShowError($"停止服务失败（卸载前）: {stopResult.ErrorMessage}");
                    return;
                }
            }

            // 卸载服务
            Status = ServiceStatus.Uninstalling;
            var uninstallResult = await _serviceManager.UninstallServiceAsync(Service);

            if (uninstallResult.Success)
            {
                Status = ServiceStatus.NotInstalled;
                MessageBox.Show($"服务 '{DisplayName}' 已成功卸载。", "卸载成功",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                Status = ServiceStatus.Stopped;
                ShowError($"卸载服务失败: {uninstallResult.ErrorMessage}");
            }
        }

        [RelayCommand]
        private void RefreshStatus()
        {
            try
            {
                IsBusy = true;
                var actualStatus = _serviceManager.GetActualServiceStatus(Service);
                Status = actualStatus;
            }
            catch (Exception ex)
            {
                ShowError($"刷新状态失败: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private void OpenWorkingDirectory()
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = WorkingDirectory,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                ShowError($"无法打开工作目录: {ex.Message}");
            }
        }

        [RelayCommand]
        private void CopyExecutablePath()
        {
            try
            {
                Clipboard.SetText(ExecutablePath);
            }
            catch (Exception ex)
            {
                ShowError($"复制路径失败: {ex.Message}");
            }
        }

        [RelayCommand(CanExecute = nameof(CanEdit))]
        private void Edit()
        {
            // 触发编辑请求事件，由主窗口处理
            EditRequested?.Invoke(this, Service);
        }

        [RelayCommand(CanExecute = nameof(CanDelete))]
        private void Delete()
        {
            // 触发删除请求事件，由主窗口处理
            DeleteRequested?.Invoke(this, Service);
        }

        [RelayCommand]
        private void CopyArguments()
        {
            try
            {
                var fullArgs = Service.GetFullArguments();

                // 使用重试机制处理剪贴板被占用的情况
                bool success = false;
                int retryCount = 0;
                const int maxRetries = 5;

                while (!success && retryCount < maxRetries)
                {
                    try
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            Clipboard.SetText(fullArgs);
                        });
                        success = true;
                    }
                    catch (Exception)
                    {
                        retryCount++;
                        if (retryCount < maxRetries)
                        {
                            // 等待 50ms 后重试
                            Thread.Sleep(50);
                        }
                        else
                        {
                            throw;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ShowError($"复制启动参数失败: {ex.Message}");
            }
        }

        [RelayCommand]
        private void ShowStartupFailureDetails()
        {
            if (LastStartupResult == StartupResult.Failed && !string.IsNullOrEmpty(LastStartupErrorMessage))
            {
                MessageBox.Show(
                    LastStartupErrorMessage,
                    $"{DisplayName} - 启动失败详情",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            else if (LastStartupResult == StartupResult.Failed)
            {
                MessageBox.Show(
                    "暂无详细错误信息",
                    $"{DisplayName} - 启动失败详情",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// 显示错误信息
        /// </summary>
        private void ShowError(string message)
        {
            MessageBox.Show(message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// 更新服务状态
        /// </summary>
        public void UpdateStatus(ServiceStatus newStatus)
        {
            var oldStatus = Status;
            Status = newStatus;
            // 添加日志验证
            System.Diagnostics.Debug.WriteLine($"[ServiceItemViewModel] UpdateStatus: {Service.Id} {oldStatus} -> {newStatus}, current Status: {Status}");
        }

        /// <summary>
        /// 刷新命令状态
        /// </summary>
        public void RefreshCommands()
        {
            StartCommand.NotifyCanExecuteChanged();
            StopCommand.NotifyCanExecuteChanged();
            RestartCommand.NotifyCanExecuteChanged();
            UninstallCommand.NotifyCanExecuteChanged();
            EditCommand.NotifyCanExecuteChanged();
            DeleteCommand.NotifyCanExecuteChanged();
        }

        /// <summary>
        /// 刷新属性显示（当 Service 对象的属性被外部更新时调用）
        /// </summary>
        public void RefreshProperties()
        {
            OnPropertyChanged(nameof(DisplayName));
            OnPropertyChanged(nameof(Description));
            OnPropertyChanged(nameof(ExecutablePath));
            OnPropertyChanged(nameof(WorkingDirectory));
            OnPropertyChanged(nameof(Arguments));
            OnPropertyChanged(nameof(DependenciesDisplay));
            OnPropertyChanged(nameof(DependenciesDetails));
            OnPropertyChanged(nameof(CreatedAt));
        }

        #endregion

        #region IDisposable

        private bool _disposed = false;

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 释放资源的具体实现
        /// </summary>
        /// <param name="disposing">是否正在释放托管资源</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                // 清理资源
                _disposed = true;
            }
        }

        #endregion
    }
}