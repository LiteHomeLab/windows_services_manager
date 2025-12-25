using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WinServiceManager.Models;
using WinServiceManager.Services;
using WinServiceManager.Views;

namespace WinServiceManager.ViewModels
{
    /// <summary>
    /// 服务过滤器枚举
    /// </summary>
    public enum ServiceFilter
    {
        /// <summary>
        /// 显示全部服务
        /// </summary>
        All = 0,

        /// <summary>
        /// 仅显示启动成功的服務
        /// </summary>
        Success = 1,

        /// <summary>
        /// 仅显示启动失败的服务
        /// </summary>
        Failed = 2,

        /// <summary>
        /// 仅显示警告状态的服务
        /// </summary>
        Warning = 3
    }

    /// <summary>
    /// 主窗口视图模型
    /// </summary>
    public partial class MainWindowViewModel : BaseViewModel, IDisposable
    {
        private readonly ServiceManagerService _serviceManager;
        private readonly ServiceStatusMonitor _statusMonitor;
        private readonly ServicePollingCoordinator _pollingCoordinator;
        private readonly ILogger<MainWindowViewModel> _logger;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ServiceDependencyValidator _dependencyValidator;
        private string _statusMessage = "就绪";
        private ServiceItemViewModel? _selectedService;
        private bool _disposed = false;
        private string _searchText = string.Empty;
        private ObservableCollection<ServiceItemViewModel> _allServices = new();
        private ServiceFilter _selectedFilter = ServiceFilter.All;
        private int _selectedFilterIndex = 0;

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
        public int AllServicesCount { get; set; }

        /// <summary>
        /// 选中的服务过滤器
        /// </summary>
        public ServiceFilter SelectedFilter
        {
            get => _selectedFilter;
            set
            {
                if (SetProperty(ref _selectedFilter, value))
                {
                    // 同步更新 SelectedFilterIndex
                    SelectedFilterIndex = (int)value;
                    FilterServices();
                }
            }
        }

        /// <summary>
        /// 选中的服务过滤器索引（用于 UI 绑定）
        /// </summary>
        public int SelectedFilterIndex
        {
            get => _selectedFilterIndex;
            set
            {
                if (SetProperty(ref _selectedFilterIndex, value) && Enum.IsDefined(typeof(ServiceFilter), value))
                {
                    // 同步更新 SelectedFilter
                    _selectedFilter = (ServiceFilter)value;
                    FilterServices();
                }
            }
        }

        public MainWindowViewModel(
            ServiceManagerService serviceManager,
            ServiceStatusMonitor statusMonitor,
            ServicePollingCoordinator pollingCoordinator,
            ILogger<MainWindowViewModel> logger,
            ILoggerFactory loggerFactory,
            ServiceDependencyValidator dependencyValidator)
        {
            _serviceManager = serviceManager ?? throw new ArgumentNullException(nameof(serviceManager));
            _statusMonitor = statusMonitor ?? throw new ArgumentNullException(nameof(statusMonitor));
            _pollingCoordinator = pollingCoordinator ?? throw new ArgumentNullException(nameof(pollingCoordinator));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
            _dependencyValidator = dependencyValidator ?? throw new ArgumentNullException(nameof(dependencyValidator));

            // 订阅全局状态更新（ServiceStatusMonitor 的 5秒轮询）
            _statusMonitor.Subscribe(OnServicesUpdated);
            _statusMonitor.StartMonitoring(intervalSeconds: 5);

            // 订阅协调器状态更新（操作后的高频轮询）
            _pollingCoordinator.ServicesUpdated += OnPollingCoordinatorServicesUpdated;

            // 订阅服务配置变更事件
            _serviceManager.ServiceConfigChanged += OnServiceConfigChanged;

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
        private async Task EditServiceAsync(ServiceItemViewModel? serviceViewModel)
        {
            if (serviceViewModel == null)
            {
                MessageBox.Show("请先选择一个服务", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // 检查服务是否可以编辑
            if (!serviceViewModel.CanEdit)
            {
                var statusText = serviceViewModel.Status switch
                {
                    ServiceStatus.Running => "运行中",
                    ServiceStatus.Starting => "启动中",
                    ServiceStatus.Stopping => "停止中",
                    ServiceStatus.Installing => "安装中",
                    ServiceStatus.Uninstalling => "卸载中",
                    _ => "未知状态"
                };

                MessageBox.Show(
                    $"无法编辑服务：服务当前状态为「{statusText}」。\n\n请先停止服务后再进行编辑。",
                    "无法编辑",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            try
            {
                StatusMessage = $"准备编辑 {serviceViewModel.DisplayName}...";

                // 创建 ViewModel
                var editLogger = App.Services?.GetRequiredService<ILogger<EditServiceViewModel>>()
                    ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<EditServiceViewModel>.Instance;
                var editViewModel = new EditServiceViewModel(serviceViewModel.Service, _serviceManager, _dependencyValidator, editLogger);

                // 创建并显示对话框
                var dialog = new EditServiceDialog(editViewModel)
                {
                    Owner = Application.Current.MainWindow
                };

                if (dialog.ShowDialog() == true)
                {
                    StatusMessage = "服务配置更新成功";
                    await RefreshServicesAsync();
                }
                else
                {
                    StatusMessage = "取消编辑服务";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"编辑服务失败: {ex.Message}";
                MessageBox.Show($"编辑服务时发生错误: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
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
        /// 过滤服务列表（同时支持搜索文本和启动结果过滤）
        /// </summary>
        private void FilterServices()
        {
            try
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Services.Clear();

                    // 首先应用启动结果过滤
                    IEnumerable<ServiceItemViewModel> filtered = _selectedFilter switch
                    {
                        ServiceFilter.Success => _allServices.Where(s => s.LastStartupResult == StartupResult.Success),
                        ServiceFilter.Failed => _allServices.Where(s => s.LastStartupResult == StartupResult.Failed),
                        ServiceFilter.Warning => _allServices.Where(s => s.LastStartupResult == StartupResult.Warning),
                        _ => _allServices
                    };

                    // 然后应用搜索文本过滤
                    if (!string.IsNullOrWhiteSpace(_searchText))
                    {
                        var searchTerm = _searchText.Trim().ToLowerInvariant();
                        filtered = filtered.Where(s =>
                            s.DisplayName.ToLowerInvariant().Contains(searchTerm) ||
                            s.Description.ToLowerInvariant().Contains(searchTerm) ||
                            s.ExecutablePath.ToLowerInvariant().Contains(searchTerm) ||
                            s.Status.ToString().ToLowerInvariant().Contains(searchTerm)
                        );
                    }

                    var filteredList = filtered.ToList();
                    foreach (var service in filteredList)
                    {
                        Services.Add(service);
                    }

                    // 更新状态消息
                    var filterText = _selectedFilter switch
                    {
                        ServiceFilter.Success => "启动成功",
                        ServiceFilter.Failed => "启动失败",
                        ServiceFilter.Warning => "警告",
                        _ => "全部"
                    };

                    var searchText = !string.IsNullOrWhiteSpace(_searchText) ? $" (搜索: {_searchText})" : "";
                    StatusMessage = $"显示 {filterText} 服务: {filteredList.Count}{searchText}";
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
        private void OpenSettings()
        {
            try
            {
                // 获取设置窗口
                var settingsWindow = App.Services?.GetService<Views.SettingsWindow>();
                if (settingsWindow != null)
                {
                    settingsWindow.Owner = Application.Current.MainWindow;
                    settingsWindow.ShowDialog();
                }
                else
                {
                    MessageBox.Show("无法打开设置窗口", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to open settings window");
                MessageBox.Show($"无法打开设置窗口: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void OpenSelectedServiceDirectory()
        {
            if (SelectedService == null)
            {
                MessageBox.Show("请先选择一个服务", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                var serviceDirectory = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "services",
                    SelectedService.Service.Id);

                if (Directory.Exists(serviceDirectory))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = serviceDirectory,
                        UseShellExecute = true
                    });

                    StatusMessage = $"已打开 {SelectedService.DisplayName} 的目录";
                    _ = Task.Delay(2000).ContinueWith(_ => StatusMessage = "就绪");
                }
                else
                {
                    MessageBox.Show(
                        $"服务目录不存在:\n{serviceDirectory}",
                        "目录不存在",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to open service directory for {ServiceName}", SelectedService.DisplayName);
                MessageBox.Show(
                    $"无法打开服务目录: {ex.Message}",
                    "错误",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
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
                // 创建新的 ViewModel 列表
                var newServices = services.Select(s =>
                {
                    var viewModel = new ServiceItemViewModel(s, _serviceManager, _pollingCoordinator);
                    viewModel.EditRequested += OnServiceEditRequested;
                    viewModel.DeleteRequested += OnServiceDeleteRequested;
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
        /// 处理服务状态更新（来自 ServiceStatusMonitor）
        /// </summary>
        private async void OnServicesUpdated(System.Collections.Generic.List<ServiceItem> services)
        {
            // 不要重建整个列表，只更新状态和属性
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                foreach (var updatedService in services)
                {
                    var existingViewModel = _allServices.FirstOrDefault(vm => vm.Service.Id == updatedService.Id);
                    if (existingViewModel != null)
                    {
                        // 更新底层 Service 对象的所有属性（包括状态）
                        existingViewModel.Service.Status = updatedService.Status;
                        existingViewModel.Service.DisplayName = updatedService.DisplayName;
                        existingViewModel.Service.Description = updatedService.Description;
                        existingViewModel.Service.ExecutablePath = updatedService.ExecutablePath;
                        existingViewModel.Service.Arguments = updatedService.Arguments;
                        existingViewModel.Service.WorkingDirectory = updatedService.WorkingDirectory;
                        existingViewModel.Service.UpdatedAt = updatedService.UpdatedAt;

                        // 更新 ViewModel 的状态属性
                        existingViewModel.UpdateStatus(updatedService.Status);

                        // 刷新显示
                        existingViewModel.RefreshProperties();
                        existingViewModel.RefreshCommands();
                    }
                    else
                    {
                        // 新服务，创建新的 ViewModel
                        var newViewModel = new ServiceItemViewModel(updatedService, _serviceManager, _pollingCoordinator);
                        newViewModel.EditRequested += OnServiceEditRequested;
                        newViewModel.DeleteRequested += OnServiceDeleteRequested;
                        _allServices.Add(newViewModel);
                    }
                }

                // 移除已删除的服务
                var removedServices = _allServices
                    .Where(vm => !services.Any(s => s.Id == vm.Service.Id))
                    .ToList();

                foreach (var removed in removedServices)
                {
                    _allServices.Remove(removed);
                }

                // 应用搜索过滤
                FilterServices();
            });
        }

        /// <summary>
        /// 处理协调器批量状态更新（来自 ServicePollingCoordinator）
        /// </summary>
        private void OnPollingCoordinatorServicesUpdated(object? sender, ServicesUpdatedEventArgs e)
        {
            _logger.LogInformation("Received ServicesUpdated event for {Count} services", e.StatusUpdates.Count);
            Application.Current.Dispatcher.Invoke(() =>
            {
                foreach (var (serviceId, status) in e.StatusUpdates)
                {
                    var viewModel = _allServices.FirstOrDefault(vm => vm.Service.Id == serviceId);
                    if (viewModel != null)
                    {
                        _logger.LogInformation("Updating service {ServiceId} status to {Status}", serviceId, status);
                        viewModel.UpdateStatus(status);
                        viewModel.RefreshCommands();
                    }
                    else
                    {
                        _logger.LogWarning("Service {ServiceId} not found in _allServices", serviceId);
                    }
                }
            });
        }

        /// <summary>
        /// 处理服务编辑请求
        /// </summary>
        private async void OnServiceEditRequested(object? sender, ServiceItem service)
        {
            try
            {
                StatusMessage = $"准备编辑 {service.DisplayName}...";

                // 查找对应的 ServiceItemViewModel
                var serviceViewModel = _allServices.FirstOrDefault(vm => vm.Service.Id == service.Id);
                if (serviceViewModel == null)
                {
                    StatusMessage = "找不到要编辑的服务";
                    return;
                }

                // 检查服务是否可以编辑
                if (!serviceViewModel.CanEdit)
                {
                    var statusText = serviceViewModel.Status switch
                    {
                        ServiceStatus.Running => "运行中",
                        ServiceStatus.Starting => "启动中",
                        ServiceStatus.Stopping => "停止中",
                        ServiceStatus.Installing => "安装中",
                        ServiceStatus.Uninstalling => "卸载中",
                        _ => "未知状态"
                    };

                    MessageBox.Show(
                        $"无法编辑服务：服务当前状态为「{statusText}」。\n\n请先停止服务后再进行编辑。",
                        "无法编辑",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    StatusMessage = "服务当前无法编辑";
                    await Task.Delay(2000);
                    StatusMessage = "就绪";
                    return;
                }

                // 创建 ViewModel
                var editLogger = App.Services?.GetRequiredService<ILogger<EditServiceViewModel>>()
                    ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<EditServiceViewModel>.Instance;
                var editViewModel = new EditServiceViewModel(service, _serviceManager, _dependencyValidator, editLogger);

                // 创建并显示对话框
                var dialog = new EditServiceDialog(editViewModel)
                {
                    Owner = Application.Current.MainWindow
                };

                if (dialog.ShowDialog() == true)
                {
                    StatusMessage = "服务配置更新成功";
                    await RefreshServicesAsync();
                }
                else
                {
                    StatusMessage = "取消编辑服务";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"编辑服务失败: {ex.Message}";
                MessageBox.Show($"编辑服务时发生错误: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                await Task.Delay(2000);
                StatusMessage = "就绪";
            }
        }

        /// <summary>
        /// 处理服务删除请求
        /// </summary>
        private async void OnServiceDeleteRequested(object? sender, ServiceItem service)
        {
            try
            {
                StatusMessage = $"准备删除 {service.DisplayName}...";

                // 查找对应的 ServiceItemViewModel
                var serviceViewModel = _allServices.FirstOrDefault(vm => vm.Service.Id == service.Id);
                if (serviceViewModel == null)
                {
                    StatusMessage = "找不到要删除的服务";
                    return;
                }

                // 检查服务是否可以删除
                if (!serviceViewModel.CanDelete)
                {
                    var statusText = serviceViewModel.Status switch
                    {
                        ServiceStatus.Running => "运行中",
                        _ => "忙碌状态"
                    };

                    MessageBox.Show(
                        $"无法删除服务：服务当前状态为「{statusText}」。\n\n请先停止服务后再进行删除。",
                        "无法删除",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    StatusMessage = "服务当前无法删除";
                    await Task.Delay(2000);
                    StatusMessage = "就绪";
                    return;
                }

                // 显示确认对话框
                var statusInfo = serviceViewModel.Status switch
                {
                    ServiceStatus.NotInstalled => "（未安装）",
                    ServiceStatus.Error => "（错误状态）",
                    ServiceStatus.Stopped => "（已停止）",
                    _ => ""
                };

                var result = MessageBox.Show(
                    $"确定要删除服务 '{service.DisplayName}' {statusInfo} 吗？\n\n此操作将:\n" +
                    $"• 从列表中移除该服务\n" +
                    $"• 删除所有相关配置文件\n\n" +
                    $"此操作不可恢复。",
                    "确认删除",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result != MessageBoxResult.Yes)
                {
                    StatusMessage = "取消删除服务";
                    await Task.Delay(1000);
                    StatusMessage = "就绪";
                    return;
                }

                // 执行删除
                var deleteResult = await _serviceManager.DeleteServiceAsync(service.Id);

                if (deleteResult.Success)
                {
                    StatusMessage = $"服务 '{service.DisplayName}' 已删除";
                    await RefreshServicesAsync();
                }
                else
                {
                    StatusMessage = $"删除服务失败: {deleteResult.ErrorMessage}";
                    MessageBox.Show(
                        $"删除服务失败: {deleteResult.ErrorMessage}",
                        "删除失败",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"删除服务失败: {ex.Message}";
                MessageBox.Show($"删除服务时发生错误: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                await Task.Delay(2000);
                StatusMessage = "就绪";
            }
        }

        /// <summary>
        /// 处理服务配置变更事件
        /// </summary>
        private async void OnServiceConfigChanged(ServiceItem updatedService)
        {
            _logger.LogInformation("服务配置已更新: ServiceId={ServiceId}, DisplayName={DisplayName}",
                updatedService.Id, updatedService.DisplayName);

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                // 查找现有的 ServiceItemViewModel
                var existingViewModel = _allServices.FirstOrDefault(vm => vm.Service.Id == updatedService.Id);
                if (existingViewModel != null)
                {
                    // 更新底层 Service 对象的所有属性
                    existingViewModel.Service.DisplayName = updatedService.DisplayName;
                    existingViewModel.Service.Description = updatedService.Description;
                    existingViewModel.Service.ExecutablePath = updatedService.ExecutablePath;
                    existingViewModel.Service.ScriptPath = updatedService.ScriptPath;
                    existingViewModel.Service.Arguments = updatedService.Arguments;
                    existingViewModel.Service.WorkingDirectory = updatedService.WorkingDirectory;
                    existingViewModel.Service.Dependencies = updatedService.Dependencies;
                    existingViewModel.Service.EnvironmentVariables = updatedService.EnvironmentVariables;
                    existingViewModel.Service.ServiceAccount = updatedService.ServiceAccount;
                    existingViewModel.Service.StartMode = updatedService.StartMode;
                    existingViewModel.Service.StopTimeout = updatedService.StopTimeout;
                    existingViewModel.Service.EnableRestartOnExit = updatedService.EnableRestartOnExit;
                    existingViewModel.Service.RestartExitCode = updatedService.RestartExitCode;
                    existingViewModel.Service.UpdatedAt = updatedService.UpdatedAt;

                    // 触发 UI 属性更新
                    existingViewModel.RefreshProperties();
                }
            });
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
                _pollingCoordinator.ServicesUpdated -= OnPollingCoordinatorServicesUpdated;

                // 取消订阅服务配置变更事件
                _serviceManager.ServiceConfigChanged -= OnServiceConfigChanged;

                // 取消订阅事件并释放资源
                foreach (var service in Services)
                {
                    if (service is ServiceItemViewModel serviceViewModel)
                    {
                        serviceViewModel.EditRequested -= OnServiceEditRequested;
                        serviceViewModel.DeleteRequested -= OnServiceDeleteRequested;
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
                        serviceViewModel.EditRequested -= OnServiceEditRequested;
                        serviceViewModel.DeleteRequested -= OnServiceDeleteRequested;
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