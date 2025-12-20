using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using WinServiceManager.Models;
using WinServiceManager.Services;
using WinServiceManager.Views;

namespace WinServiceManager.ViewModels
{
    /// <summary>
    /// 主窗口视图模型
    /// </summary>
    public partial class MainWindowViewModel : BaseViewModel, IDisposable
    {
        private readonly ServiceManagerService _serviceManager;
        private readonly ServiceStatusMonitor _statusMonitor;
        private readonly LogReaderService _logReaderService;
        private readonly ILogger<MainWindowViewModel> _logger;
        private readonly ServiceDependencyValidator _dependencyValidator;
        private string _statusMessage = "就绪";
        private ServiceItemViewModel? _selectedService;
        private bool _disposed = false;
        private string _searchText = string.Empty;
        private ObservableCollection<ServiceItemViewModel> _allServices = new();

        /// <summary>
        /// 服务项视图模型集合
        /// </summary>
        public ObservableCollection<ServiceItemViewModel> Services { get; } = new();

        /// <summary>
        /// 搜索文本
        /// </summary>
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    FilterServices();
                }
            }
        }

        /// <summary>
        /// 状态消息
        /// </summary>
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        /// <summary>
        /// 选中的服务
        /// </summary>
        public ServiceItemViewModel? SelectedService
        {
            get => _selectedService;
            set => SetProperty(ref _selectedService, value);
        }

        /// <summary>
        /// 所有服务数量
        /// </summary>
        public int AllServicesCount { get; private set; }

        public MainWindowViewModel(
            ServiceManagerService serviceManager,
            ServiceStatusMonitor statusMonitor,
            LogReaderService logReaderService,
            ILogger<MainWindowViewModel> logger,
            ServiceDependencyValidator dependencyValidator)
        {
            _serviceManager = serviceManager ?? throw new ArgumentNullException(nameof(serviceManager));
            _statusMonitor = statusMonitor ?? throw new ArgumentNullException(nameof(statusMonitor));
            _logReaderService = logReaderService ?? throw new ArgumentNullException(nameof(logReaderService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _dependencyValidator = dependencyValidator ?? throw new ArgumentNullException(nameof(dependencyValidator));

            // 订阅状态更新
            _statusMonitor.Subscribe(OnServicesUpdated);

            // 初始加载
            _ = RefreshServicesAsync();
        }

        #region Commands

        [RelayCommand]
        private async Task CreateServiceAsync()
        {
            try
            {
                StatusMessage = "准备创建服务...";

                // 创建 ViewModel
                var createViewModel = new CreateServiceViewModel(_serviceManager, _dependencyValidator);

                // 创建并显示对话框
                var dialog = new CreateServiceDialog(createViewModel)
                {
                    Owner = Application.Current.MainWindow
                };

                if (dialog.ShowDialog() == true)
                {
                    StatusMessage = "服务创建成功";
                    await RefreshServicesAsync();
                }
                else
                {
                    StatusMessage = "取消创建服务";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"创建服务失败: {ex.Message}";
                MessageBox.Show($"创建服务时发生错误: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                await Task.Delay(2000);
                StatusMessage = "就绪";
            }
        }

        [RelayCommand]
        private async Task RefreshServicesAsync()
        {
            try
            {
                StatusMessage = "正在刷新服务列表...";

                var services = await _serviceManager.GetAllServicesAsync();
                await UpdateServicesAsync(services);

                StatusMessage = $"已刷新 {services.Count} 个服务";
            }
            catch (Exception ex)
            {
                StatusMessage = $"刷新失败: {ex.Message}";
                MessageBox.Show($"刷新服务列表失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                await Task.Delay(2000);
                StatusMessage = "就绪";
            }
        }

        [RelayCommand]
        private async Task ExportServicesAsync()
        {
            try
            {
                var saveDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Title = "导出服务配置",
                    Filter = "JSON文件 (*.json)|*.json|所有文件 (*.*)|*.*",
                    FileName = $"services_{DateTime.Now:yyyyMMdd_HHmmss}.json"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    StatusMessage = "正在导出服务配置...";

                    // 创建导出数据结构
                    var exportData = new
                    {
                        ExportTime = DateTime.UtcNow,
                        ExportVersion = "1.0",
                        Services = _allServices.Select(vm => vm.Service).Select(s => new
                        {
                            s.Id,
                            s.DisplayName,
                            s.Description,
                            s.ExecutablePath,
                            s.ScriptPath,
                            s.WorkingDirectory,
                            s.StartupArguments,
                            s.ServiceAccount,
                            Environment = s.Environment?.ToDictionary(
                                e => e.Key,
                                e => e.Value
                            ),
                            LogPath = s.LogPath,
                            LogMode = s.LogMode.ToString(),
                            s.StartMode,
                            s.StopTimeout,
                            s.Priority,
                            s.Affinity,
                            CreatedAt = s.CreatedAt.ToUniversalTime(),
                            s.Metadata
                        }).ToList()
                    };

                    // 序列化为 JSON
                    var jsonOptions = new System.Text.Json.JsonSerializerOptions
                    {
                        WriteIndented = true,
                        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                    };

                    var json = System.Text.Json.JsonSerializer.Serialize(exportData, jsonOptions);

                    // 写入文件
                    await File.WriteAllTextAsync(saveDialog.FileName, json);

                    StatusMessage = $"成功导出 {_allServices.Count} 个服务配置到 {Path.GetFileName(saveDialog.FileName)}";

                    // 询问是否打开文件所在目录
                    var result = MessageBox.Show(
                        "导出成功！是否打开文件所在目录？",
                        "导出完成",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        var folder = Path.GetDirectoryName(saveDialog.FileName);
                        if (!string.IsNullOrEmpty(folder))
                        {
                            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                            {
                                FileName = folder,
                                UseShellExecute = true
                            });
                        }
                    }
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                StatusMessage = "导出失败：没有写入权限";
                MessageBox.Show("没有权限写入到指定位置，请选择其他位置", "权限错误", MessageBoxButton.OK, MessageBoxImage.Error);
                _logger.LogError(ex, "导出服务配置时发生权限错误");
            }
            catch (Exception ex)
            {
                StatusMessage = $"导出失败: {ex.Message}";
                MessageBox.Show($"导出服务配置失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                _logger.LogError(ex, "导出服务配置时发生错误");
            }
            finally
            {
                await Task.Delay(3000);
                StatusMessage = "就绪";
            }
        }

        [RelayCommand]
        private void OpenServicesFolder()
        {
            try
            {
                var servicesPath = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "services");

                if (Directory.Exists(servicesPath))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = servicesPath,
                        UseShellExecute = true
                    });
                }
                else
                {
                    MessageBox.Show("服务目录不存在", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"无法打开服务目录: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void Search()
        {
            FilterServices();
        }

        /// <summary>
        /// 过滤服务列表
        /// </summary>
        private void FilterServices()
        {
            try
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Services.Clear();

                    if (string.IsNullOrWhiteSpace(_searchText))
                    {
                        // 显示所有服务
                        foreach (var service in _allServices)
                        {
                            Services.Add(service);
                        }
                        StatusMessage = $"显示所有服务 ({_allServices.Count})";
                    }
                    else
                    {
                        // 过滤服务
                        var searchTerm = _searchText.Trim().ToLowerInvariant();
                        var filteredServices = _allServices.Where(s =>
                            s.DisplayName.ToLowerInvariant().Contains(searchTerm) ||
                            s.Description.ToLowerInvariant().Contains(searchTerm) ||
                            s.ExecutablePath.ToLowerInvariant().Contains(searchTerm) ||
                            s.Status.ToString().ToLowerInvariant().Contains(searchTerm)
                        ).ToList();

                        foreach (var service in filteredServices)
                        {
                            Services.Add(service);
                        }

                        StatusMessage = $"找到 {filteredServices.Count} 个匹配的服务";
                    }
                });

                // 2秒后恢复默认状态消息
                _ = Task.Delay(2000).ContinueWith(_ => StatusMessage = "就绪");
            }
            catch (Exception ex)
            {
                StatusMessage = $"搜索失败: {ex.Message}";
                _logger.LogError(ex, "搜索服务时发生错误");
            }
        }

        [RelayCommand]
        private async Task ViewLogsAsync()
        {
            if (SelectedService == null)
            {
                MessageBox.Show("请先选择一个服务", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                StatusMessage = $"准备查看 {SelectedService.DisplayName} 的日志...";

                // 创建 ViewModel
                var logViewModel = new LogViewerViewModel(_logReaderService, SelectedService.Service);

                // 创建并显示窗口
                var logWindow = new LogViewerWindow(SelectedService.Service, logViewModel)
                {
                    Owner = Application.Current.MainWindow
                };

                logWindow.Show();

                StatusMessage = "日志查看器已打开";
            }
            catch (Exception ex)
            {
                StatusMessage = $"打开日志查看器失败: {ex.Message}";
                MessageBox.Show($"无法打开日志查看器: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                await Task.Delay(2000);
                StatusMessage = "就绪";
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// 更新服务列表
        /// </summary>
        private async Task UpdateServicesAsync(System.Collections.Generic.List<ServiceItem> services)
        {
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                // 创建新的 ViewModel 列表并订阅ViewLogsRequested事件
                var newServices = services.Select(s =>
                {
                    var viewModel = new ServiceItemViewModel(s, _serviceManager);
                    viewModel.ViewLogsRequested += OnServiceViewLogsRequested;
                    return viewModel;
                }).ToList();

                // 更新完整服务集合
                _allServices.Clear();
                foreach (var service in newServices)
                {
                    _allServices.Add(service);
                }

                // 应用搜索过滤
                FilterServices();

                AllServicesCount = services.Count;
                OnPropertyChanged(nameof(AllServicesCount));
            });
        }

        /// <summary>
        /// 处理服务状态更新
        /// </summary>
        private async void OnServicesUpdated(System.Collections.Generic.List<ServiceItem> services)
        {
            await UpdateServicesAsync(services);
        }

        /// <summary>
        /// 处理服务查看日志请求
        /// </summary>
        private async void OnServiceViewLogsRequested(object? sender, ServiceItem service)
        {
            try
            {
                StatusMessage = $"准备查看 {service.DisplayName} 的日志...";

                // 创建 ViewModel
                var logViewModel = new LogViewerViewModel(_logReaderService, service);

                // 创建并显示窗口
                var logWindow = new LogViewerWindow(service, logViewModel)
                {
                    Owner = Application.Current.MainWindow
                };

                logWindow.Show();

                StatusMessage = "日志查看器已打开";
            }
            catch (Exception ex)
            {
                StatusMessage = $"打开日志查看器失败: {ex.Message}";
                MessageBox.Show($"无法打开日志查看器: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                await Task.Delay(2000);
                StatusMessage = "就绪";
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// 排序服务
        /// </summary>
        public void SortServices(string columnName)
        {
            try
            {
                // 对 AllServices 进行排序
                var sorted = _allServices.ToList();

                switch (columnName.ToLowerInvariant())
                {
                    case "name":
                        sorted = sorted.OrderBy(s => s.DisplayName).ToList();
                        break;
                    case "status":
                        sorted = sorted.OrderBy(s => s.Status).ToList();
                        break;
                    case "created":
                        sorted = sorted.OrderByDescending(s => s.CreatedAt).ToList();
                        break;
                    default:
                        StatusMessage = $"未知的排序列: {columnName}";
                        _ = Task.Delay(2000).ContinueWith(_ => StatusMessage = "就绪");
                        return;
                }

                // 更新 AllServices
                _allServices.Clear();
                foreach (var service in sorted)
                {
                    _allServices.Add(service);
                }

                // 重新应用过滤
                FilterServices();

                StatusMessage = $"已按 {columnName} 排序";
                _ = Task.Delay(2000).ContinueWith(_ => StatusMessage = "就绪");
            }
            catch (Exception ex)
            {
                StatusMessage = $"排序失败: {ex.Message}";
                _logger.LogError(ex, "排序服务时发生错误");
            }
        }

        #endregion

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
                // 取消订阅状态监控
                _statusMonitor?.Unsubscribe(OnServicesUpdated);

                // 取消订阅ViewLogsRequested事件并释放资源
                foreach (var service in Services)
                {
                    if (service is ServiceItemViewModel serviceViewModel)
                    {
                        serviceViewModel.ViewLogsRequested -= OnServiceViewLogsRequested;
                    }

                    if (service is IDisposable disposableService)
                    {
                        disposableService.Dispose();
                    }
                }

                foreach (var service in _allServices)
                {
                    if (service is ServiceItemViewModel serviceViewModel)
                    {
                        serviceViewModel.ViewLogsRequested -= OnServiceViewLogsRequested;
                    }

                    if (service is IDisposable disposableService)
                    {
                        disposableService.Dispose();
                    }
                }

                Services.Clear();
                _allServices.Clear();
                _disposed = true;
            }
        }

        #endregion
    }
}