# MVVM 架构设计

## MVVM 模式概述

WinServiceManager 采用 MVVM (Model-View-ViewModel) 架构模式，将用户界面与业务逻辑分离，提高代码的可测试性和可维护性。我们使用 CommunityToolkit.Mvvm 作为 MVVM 框架，它提供了丰富的工具类和属性来简化 MVVM 实现。

## 1. 基础 MVVM 配置

### 安装 NuGet 包
```xml
<ItemGroup>
  <PackageReference Include="CommunityToolkit.Mvvm" Version="8.2.2" />
  <PackageReference Include="Microsoft.Xaml.Behaviors.Wpf" Version="1.1.77" />
</ItemGroup>
```

### 创建 ViewModel 基类
```csharp
// File: ViewModels/BaseViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace WinServiceManager.ViewModels
{
    /// <summary>
    /// ViewModel 基类
    /// </summary>
    public partial class BaseViewModel : ObservableObject
    {
        [ObservableProperty]
        private bool _isBusy;

        [ObservableProperty]
        private string _busyMessage = string.Empty;

        /// <summary>
        /// 设置忙碌状态
        /// </summary>
        protected void SetBusy(bool isBusy, string message = "")
        {
            IsBusy = isBusy;
            BusyMessage = message;
        }

        /// <summary>
        /// 异步执行操作并自动管理忙碌状态
        /// </summary>
        protected async Task ExecuteBusyActionAsync(Func<Task> action, string busyMessage = "处理中...")
        {
            try
            {
                SetBusy(true, busyMessage);
                await action();
            }
            finally
            {
                SetBusy(false);
            }
        }
    }
}
```

## 2. MainWindowViewModel - 主窗口视图模型

### 类定义
```csharp
// File: ViewModels/MainWindowViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using WinServiceManager.Models;
using WinServiceManager.Services;

namespace WinServiceManager.ViewModels
{
    /// <summary>
    /// 主窗口视图模型
    /// </summary>
    public partial class MainWindowViewModel : BaseViewModel
    {
        private readonly ServiceManagerService _serviceManager;
        private readonly LogReaderService _logReaderService;

        [ObservableProperty]
        private ObservableCollection<ServiceItemViewModel> _services;

        [ObservableProperty]
        private ServiceItemViewModel? _selectedService;

        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private bool _isRefreshing;

        private ObservableCollection<ServiceItemViewModel> _allServices;

        public MainWindowViewModel(
            ServiceManagerService serviceManager,
            LogReaderService logReaderService)
        {
            _serviceManager = serviceManager;
            _logReaderService = logReaderService;

            _allServices = new ObservableCollection<ServiceItemViewModel>();
            _services = new ObservableCollection<ServiceItemViewModel>();

            InitializeServices();
        }

        private void InitializeServices()
        {
            // 订阅服务管理器的事件
            _serviceManager.Services.CollectionChanged += OnServicesCollectionChanged;

            // 初始化服务列表
            LoadServices();
        }

        private void LoadServices()
        {
            _allServices.Clear();

            foreach (var service in _serviceManager.Services)
            {
                var vm = new ServiceItemViewModel(service, _serviceManager);
                _allServices.Add(vm);
            }

            ApplySearchFilter();
        }

        private void OnServicesCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                switch (e.Action)
                {
                    case System.Collections.Specialized.NotifyCollectionChangedAction.Add:
                        foreach (ServiceItem item in e.NewItems!)
                        {
                            var vm = new ServiceItemViewModel(item, _serviceManager);
                            _allServices.Add(vm);
                        }
                        break;

                    case System.Collections.Specialized.NotifyCollectionChangedAction.Remove:
                        foreach (ServiceItem item in e.OldItems!)
                        {
                            var vm = _allServices.FirstOrDefault(s => s.Id == item.Id);
                            if (vm != null)
                                _allServices.Remove(vm);
                        }
                        break;

                    case System.Collections.Specialized.NotifyCollectionChangedAction.Reset:
                        LoadServices();
                        break;
                }

                ApplySearchFilter();
            });
        }

        partial void OnSearchTextChanged(string value)
        {
            ApplySearchFilter();
        }

        private void ApplySearchFilter()
        {
            Services.Clear();

            if (string.IsNullOrWhiteSpace(SearchText))
            {
                foreach (var service in _allServices)
                {
                    Services.Add(service);
                }
            }
            else
            {
                var filtered = _allServices.Where(s =>
                    s.DisplayName.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                    s.Description.Contains(SearchText, StringComparison.OrdinalIgnoreCase));

                foreach (var service in filtered)
                {
                    Services.Add(service);
                }
            }
        }

        [RelayCommand]
        private async Task CreateServiceAsync()
        {
            var dialog = new ServiceCreateDialog(_serviceManager);

            if (dialog.ShowDialog() == true)
            {
                // 服务创建成功后，无需手动刷新，CollectionChanged 事件会处理
                await Task.CompletedTask;
            }
        }

        [RelayCommand]
        private async Task RefreshServicesAsync()
        {
            if (IsRefreshing) return;

            await ExecuteBusyActionAsync(async () =>
            {
                IsRefreshing = true;
                await _serviceManager.RefreshAllServicesStatusAsync();

                // 更新每个服务 ViewModel 的状态
                foreach (var vm in _allServices)
                {
                    vm.RefreshStatus();
                }
            }, "刷新服务状态...");
        }

        [RelayCommand]
        private void ViewLogs()
        {
            if (SelectedService == null) return;

            var window = new LogViewerWindow(SelectedService, _logReaderService);
            window.Show();
        }

        [RelayCommand]
        private async Task UninstallServiceAsync()
        {
            if (SelectedService == null) return;

            var result = await ShowConfirmDialog(
                $"确定要卸载服务 '{SelectedService.DisplayName}' 吗？",
                "确认卸载",
                "这将完全移除服务并删除所有相关文件。");

            if (result)
            {
                var operationResult = await SelectedService.UninstallAsync();

                if (!operationResult.Success)
                {
                    await ShowError($"卸载失败: {operationResult.ErrorMessage}");
                }
            }
        }

        [RelayCommand]
        private void OpenServicesFolder()
        {
            var servicesPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "services");

            if (Directory.Exists(servicesPath))
            {
                Process.Start("explorer.exe", servicesPath);
            }
        }

        [RelayCommand]
        private async Task ExportServicesAsync()
        {
            var saveDialog = new SaveFileDialog
            {
                Filter = "JSON 文件|*.json",
                Title = "导出服务配置",
                FileName = $"services_backup_{DateTime.Now:yyyyMMdd_HHmmss}.json"
            };

            if (saveDialog.ShowDialog() == true)
            {
                // TODO: 实现导出功能
                await Task.CompletedTask;
            }
        }

        private async Task<bool> ShowConfirmDialog(string message, string title, string detail = "")
        {
            var result = MessageBox.Show(
                detail.IsEmpty() ? message : $"{message}\n\n{detail}",
                title,
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            return result == MessageBoxResult.Yes;
        }

        private async Task ShowError(string message)
        {
            await Task.Run(() => MessageBox.Show(
                message,
                "错误",
                MessageBoxButton.OK,
                MessageBoxImage.Error));
        }
    }
}
```

## 3. ServiceItemViewModel - 服务项视图模型

### 类定义
```csharp
// File: ViewModels/ServiceItemViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinServiceManager.Models;
using WinServiceManager.Services;

namespace WinServiceManager.ViewModels
{
    /// <summary>
    /// 服务项视图模型
    /// </summary>
    public partial class ServiceItemViewModel : ObservableObject
    {
        private readonly ServiceManagerService _serviceManager;

        [ObservableProperty]
        private ServiceItem _service;

        [ObservableProperty]
        private bool _isExpanded;

        [ObservableProperty]
        private string _statusDisplay;

        [ObservableProperty]
        private System.Windows.Media.SolidColorBrush _statusColor;

        public ServiceItemViewModel(ServiceItem service, ServiceManagerService serviceManager)
        {
            _service = service;
            _serviceManager = serviceManager;

            UpdateStatusDisplay();
        }

        public string Id => Service.Id;
        public string DisplayName => Service.DisplayName;
        public string Description => Service.Description ?? "无描述";
        public string ExecutablePath => Service.ExecutablePath;
        public string WorkingDirectory => Service.WorkingDirectory;
        public DateTime CreatedAt => Service.CreatedAt;
        public DateTime UpdatedAt => Service.UpdatedAt;

        #region Commands

        [RelayCommand]
        private async Task StartAsync()
        {
            var result = await _serviceManager.StartServiceAsync(Service.Id);
            if (!result.Success)
            {
                await ShowError($"启动服务失败: {result.ErrorMessage}");
            }
        }

        [RelayCommand]
        private async Task StopAsync()
        {
            var result = await _serviceManager.StopServiceAsync(Service.Id);
            if (!result.Success)
            {
                await ShowError($"停止服务失败: {result.ErrorMessage}");
            }
        }

        [RelayCommand]
        private async Task RestartAsync()
        {
            var result = await _serviceManager.RestartServiceAsync(Service.Id);
            if (!result.Success)
            {
                await ShowError($"重启服务失败: {result.ErrorMessage}");
            }
        }

        [RelayCommand]
        private async Task UninstallAsync()
        {
            var result = await _serviceManager.UninstallServiceAsync(Service.Id);
            if (!result.Success)
            {
                await ShowError($"卸载服务失败: {result.ErrorMessage}");
            }
        }

        [RelayCommand]
        private void Edit()
        {
            // TODO: 打开编辑对话框
        }

        [RelayCommand]
        private void ViewLogs()
        {
            // TODO: 打开日志查看器
        }

        [RelayCommand]
        private void OpenWorkingDirectory()
        {
            if (Directory.Exists(Service.WorkingDirectory))
            {
                Process.Start("explorer.exe", Service.WorkingDirectory);
            }
        }

        [RelayCommand]
        private void CopyExecutablePath()
        {
            Clipboard.SetText(Service.ExecutablePath);
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// 刷新服务状态显示
        /// </summary>
        public void RefreshStatus()
        {
            var newStatus = _serviceManager.GetServiceStatus(Service.Id);
            if (Service.Status != newStatus)
            {
                Service.Status = newStatus;
                Service.UpdatedAt = DateTime.Now;
                UpdateStatusDisplay();
            }
        }

        #endregion

        #region Private Methods

        private void UpdateStatusDisplay()
        {
            StatusDisplay = Service.Status.GetDisplayText();
            StatusColor = Service.Status switch
            {
                ServiceStatus.Running => new System.Windows.Media.SolidColorBrush(System.Windows.Colors.Green),
                ServiceStatus.Stopped => new System.Windows.Media.SolidColorBrush(System.Windows.Colors.Gray),
                ServiceStatus.Starting or ServiceStatus.Stopping => new System.Windows.Media.SolidColorBrush(System.Windows.Colors.Orange),
                ServiceStatus.Error => new System.Windows.Media.SolidColorBrush(System.Windows.Colors.Red),
                _ => new System.Windows.Media.SolidColorBrush(System.Windows.Colors.Black)
            };
        }

        private async Task ShowError(string message)
        {
            await Task.Run(() => MessageBox.Show(
                message,
                "错误",
                MessageBoxButton.OK,
                MessageBoxImage.Error));
        }

        #endregion

        #region Computed Properties

        public bool CanStart => Service.Status.CanStart() && !Service.Status.IsTransitioning();
        public bool CanStop => Service.Status.CanStop() && !Service.Status.IsTransitioning();
        public bool CanRestart => Service.Status.CanStop() && !Service.Status.IsTransitioning();
        public bool CanUninstall => Service.Status.CanUninstall() && !Service.Status.IsTransitioning();
        public bool IsTransitioning => Service.Status.IsTransitioning();

        #endregion
    }
}
```

## 4. ServiceCreateViewModel - 创建服务视图模型

### 类定义
```csharp
// File: ViewModels/ServiceCreateViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using WinServiceManager.Models;
using WinServiceManager.Services;

namespace WinServiceManager.ViewModels
{
    /// <summary>
    /// 创建服务视图模型
    /// </summary>
    public partial class ServiceCreateViewModel : BaseViewModel
    {
        private readonly ServiceManagerService _serviceManager;

        [ObservableProperty]
        private string _displayName = string.Empty;

        [ObservableProperty]
        private string _description = string.Empty;

        [ObservableProperty]
        private string _executablePath = string.Empty;

        [ObservableProperty]
        private string _scriptPath = string.Empty;

        [ObservableProperty]
        private string _arguments = string.Empty;

        [ObservableProperty]
        private string _workingDirectory = string.Empty;

        [ObservableProperty]
        private bool _autoStart = true;

        [ObservableProperty]
        private string _errorMessage = string.Empty;

        private ServiceOperationResult? _createResult;

        public ServiceCreateViewModel(ServiceManagerService serviceManager)
        {
            _serviceManager = serviceManager;
        }

        #region Commands

        [RelayCommand]
        private void BrowseExecutable()
        {
            var dialog = new OpenFileDialog
            {
                Title = "选择可执行文件",
                Filter = "可执行文件 (*.exe;*.bat;*.cmd)|*.exe;*.bat;*.cmd|所有文件 (*.*)|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                ExecutablePath = dialog.FileName;

                // 自动设置工作目录
                if (string.IsNullOrEmpty(WorkingDirectory))
                {
                    WorkingDirectory = Path.GetDirectoryName(ExecutablePath)!;
                }

                // 如果是 Python 解释器，提示选择脚本
                if (ExecutablePath.EndsWith("python.exe", StringComparison.OrdinalIgnoreCase))
                {
                    BrowseScript();
                }

                ValidateForm();
            }
        }

        [RelayCommand]
        private void BrowseScript()
        {
            var dialog = new OpenFileDialog
            {
                Title = "选择脚本文件",
                Filter = "Python 脚本 (*.py)|*.py|批处理文件 (*.bat;*.cmd)|*.bat;*.cmd|所有文件 (*.*)|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                ScriptPath = dialog.FileName;

                // 如果是脚本，自动设置工作目录为脚本所在目录
                if (!string.IsNullOrEmpty(ScriptPath))
                {
                    WorkingDirectory = Path.GetDirectoryName(ScriptPath)!;
                }

                ValidateForm();
            }
        }

        [RelayCommand]
        private void BrowseWorkingDirectory()
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "选择工作目录",
                ShowNewFolderButton = true,
                SelectedPath = WorkingDirectory
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                WorkingDirectory = dialog.SelectedPath;
                ValidateForm();
            }
        }

        [RelayCommand]
        private async Task CreateAsync()
        {
            if (!ValidateForm())
            {
                return;
            }

            var request = new ServiceCreateRequest
            {
                DisplayName = DisplayName.Trim(),
                Description = Description?.Trim(),
                ExecutablePath = ExecutablePath.Trim(),
                ScriptPath = string.IsNullOrEmpty(ScriptPath) ? null : ScriptPath.Trim(),
                Arguments = Arguments.Trim(),
                WorkingDirectory = WorkingDirectory.Trim(),
                AutoStart = AutoStart
            };

            await ExecuteBusyActionAsync(async () =>
            {
                _createResult = await _serviceManager.CreateServiceAsync(request);

                if (_createResult.Success)
                {
                    // 创建成功，关闭窗口
                    System.Windows.Window.GetWindow(this)?.Close();
                }
                else
                {
                    ErrorMessage = _createResult.ErrorMessage ?? "创建服务失败";
                }
            }, "正在创建服务...");
        }

        [RelayCommand]
        private void Cancel()
        {
            System.Windows.Window.GetWindow(this)?.Close();
        }

        #endregion

        #region Validation

        private bool ValidateForm()
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(DisplayName))
            {
                errors.Add("服务名称不能为空");
            }
            else if (DisplayName.Length < 3 || DisplayName.Length > 100)
            {
                errors.Add("服务名称长度必须在3-100个字符之间");
            }

            if (string.IsNullOrWhiteSpace(ExecutablePath))
            {
                errors.Add("请选择可执行文件");
            }
            else if (!File.Exists(ExecutablePath))
            {
                errors.Add("指定的可执行文件不存在");
            }

            if (!string.IsNullOrEmpty(ScriptPath) && !File.Exists(ScriptPath))
            {
                errors.Add("指定的脚本文件不存在");
            }

            if (string.IsNullOrWhiteSpace(WorkingDirectory))
            {
                errors.Add("请选择工作目录");
            }
            else if (!Directory.Exists(WorkingDirectory))
            {
                errors.Add("指定的工作目录不存在");
            }

            ErrorMessage = errors.Count > 0 ? string.Join("\n", errors) : string.Empty;
            return errors.Count == 0;
        }

        #endregion

        #region Event Handlers

        partial void OnDisplayNameChanged(string value)
        {
            ErrorMessage = string.Empty;
        }

        partial void OnExecutablePathChanged(string value)
        {
            ErrorMessage = string.Empty;
        }

        partial void OnScriptPathChanged(string value)
        {
            ErrorMessage = string.Empty;
        }

        partial void OnWorkingDirectoryChanged(string value)
        {
            ErrorMessage = string.Empty;
        }

        #endregion

        #region Computed Properties

        public bool CanCreate => !IsBusy && !string.IsNullOrWhiteSpace(DisplayName) &&
                               !string.IsNullOrWhiteSpace(ExecutablePath) &&
                               !string.IsNullOrWhiteSpace(WorkingDirectory);

        #endregion
    }
}
```

## 5. LogViewerViewModel - 日志查看器视图模型

### 类定义
```csharp
// File: ViewModels/LogViewerViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.IO;
using WinServiceManager.Services;

namespace WinServiceManager.ViewModels
{
    /// <summary>
    /// 日志查看器视图模型
    /// </summary>
    public partial class LogViewerViewModel : BaseViewModel
    {
        private readonly LogReaderService _logReaderService;
        private readonly ServiceItemViewModel _serviceViewModel;
        private readonly Timer _refreshTimer;

        [ObservableProperty]
        private ObservableCollection<string> _logLines;

        [ObservableProperty]
        private string _selectedLogType = "Output";

        [ObservableProperty]
        private bool _autoScroll = true;

        [ObservableProperty]
        private bool _isAutoRefreshEnabled = true;

        [ObservableProperty]
        private int _refreshInterval = 5; // 秒

        private int _lineLimit = 1000;

        public LogViewerViewModel(ServiceItemViewModel serviceViewModel, LogReaderService logReaderService)
        {
            _serviceViewModel = serviceViewModel;
            _logReaderService = logReaderService;

            _logLines = new ObservableCollection<string>();

            _refreshTimer = new Timer(OnRefreshTimer, null, Timeout.Infinite, Timeout.Infinite);

            // 初始加载
            _ = LoadLogsAsync();

            // 启动自动刷新
            if (IsAutoRefreshEnabled)
            {
                StartAutoRefresh();
            }
        }

        #region Commands

        [RelayCommand]
        private async Task RefreshAsync()
        {
            await LoadLogsAsync();
        }

        [RelayCommand]
        private async Task ClearLogsAsync()
        {
            var result = MessageBox.Show(
                "确定要清空日志文件吗？此操作不可恢复。",
                "确认清空",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    var logPath = GetLogPath();
                    if (File.Exists(logPath))
                    {
                        await File.WriteAllTextAsync(logPath, string.Empty);
                        await LoadLogsAsync();
                    }
                }
                catch (Exception ex)
                {
                    await ShowError($"清空日志失败: {ex.Message}");
                }
            }
        }

        [RelayCommand]
        private void SaveLogs()
        {
            var saveDialog = new SaveFileDialog
            {
                Filter = "文本文件 (*.txt)|*.txt|所有文件 (*.*)|*.*",
                FileName = $"{_serviceViewModel.DisplayName}_{SelectedLogType}_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
            };

            if (saveDialog.ShowDialog() == true)
            {
                try
                {
                    File.WriteAllLines(saveDialog.FileName, LogLines);
                }
                catch (Exception ex)
                {
                    ShowError($"保存日志失败: {ex.Message}");
                }
            }
        }

        [RelayCommand]
        private void IncreaseLineLimit()
        {
            _lineLimit = Math.Min(_lineLimit * 2, 10000);
            _ = LoadLogsAsync();
        }

        [RelayCommand]
        private void DecreaseLineLimit()
        {
            _lineLimit = Math.Max(_lineLimit / 2, 100);
            _ = LoadLogsAsync();
        }

        #endregion

        #region Private Methods

        private async Task LoadLogsAsync()
        {
            try
            {
                var logPath = GetLogPath();
                var lines = await _logReaderService.ReadLastLinesAsync(logPath, _lineLimit);

                App.Current.Dispatcher.Invoke(() =>
                {
                    LogLines.Clear();
                    foreach (var line in lines)
                    {
                        LogLines.Add(line);
                    }

                    if (AutoScroll && LogLines.Count > 0)
                    {
                        // 通知滚动到底部
                        ScrollToBottomRequested?.Invoke(this, EventArgs.Empty);
                    }
                });
            }
            catch (Exception ex)
            {
                App.Current.Dispatcher.Invoke(() =>
                {
                    LogLines.Clear();
                    LogLines.Add($"读取日志失败: {ex.Message}");
                });
            }
        }

        private string GetLogPath()
        {
            return SelectedLogType switch
            {
                "Error" => Path.Combine(_serviceViewModel.ServiceDirectory, "logs", $"{_serviceViewModel.Id}.err.log"),
                _ => Path.Combine(_serviceViewModel.ServiceDirectory, "logs", $"{_serviceViewModel.Id}.out.log")
            };
        }

        private async void OnRefreshTimer(object? state)
        {
            await LoadLogsAsync();
        }

        private void StartAutoRefresh()
        {
            _refreshTimer.Change(TimeSpan.FromSeconds(_refreshInterval), TimeSpan.FromSeconds(_refreshInterval));
        }

        private void StopAutoRefresh()
        {
            _refreshTimer.Change(Timeout.Infinite, Timeout.Infinite);
        }

        private async Task ShowError(string message)
        {
            await Task.Run(() => MessageBox.Show(
                message,
                "错误",
                MessageBoxButton.OK,
                MessageBoxImage.Error));
        }

        #endregion

        #region Event Handlers

        partial void OnSelectedLogTypeChanged(string value)
        {
            _ = LoadLogsAsync();
        }

        partial void OnIsAutoRefreshEnabledChanged(bool value)
        {
            if (value)
            {
                StartAutoRefresh();
            }
            else
            {
                StopAutoRefresh();
            }
        }

        partial void OnRefreshIntervalChanged(int value)
        {
            if (IsAutoRefreshEnabled)
            {
                StopAutoRefresh();
                StartAutoRefresh();
            }
        }

        #endregion

        #region Events

        /// <summary>
        /// 请求滚动到底部事件
        /// </summary>
        public event EventHandler? ScrollToBottomRequested;

        #endregion

        #region IDisposable

        public void Dispose()
        {
            _refreshTimer?.Dispose();
        }

        #endregion
    }
}
```

## 6. View 和 ViewModel 的绑定

### 在 XAML 中绑定
```xml
<!-- MainWindow.xaml -->
<Window.DataContext>
    <local:MainWindowViewModel/>
</Window.DataContext>

<Grid>
    <ListBox ItemsSource="{Binding Services}"
             SelectedItem="{Binding SelectedService}">
        <ListBox.ItemTemplate>
            <DataTemplate>
                <StackPanel>
                    <StackPanel Orientation="Horizontal">
                        <Ellipse Width="10" Height="10"
                                Fill="{Binding StatusColor}"
                                Margin="0,0,5,0"/>
                        <TextBlock Text="{Binding DisplayName}"/>
                    </StackPanel>
                    <TextBlock Text="{Binding StatusDisplay}"
                               FontSize="12"
                               Foreground="Gray"/>
                </StackPanel>
            </DataTemplate>
        </ListBox.ItemTemplate>
    </ListBox>
</Grid>
```

### 命令绑定
```xml
<Button Content="创建服务"
        Command="{Binding CreateServiceCommand}"/>

<Button Content="启动"
        Command="{Binding SelectedService.StartCommand}"
        CommandParameter="{Binding SelectedService}"/>
```

## 7. 依赖注入配置

### 在 App.xaml.cs 中注册 ViewModel
```csharp
protected override void OnStartup(StartupEventArgs e)
{
    base.OnStartup(e);

    var services = new ServiceCollection();

    // 注册服务
    services.AddLogging(builder => builder.AddConsole());
    services.AddSingleton<WinSWWrapper>();
    services.AddSingleton<DataService>();
    services.AddSingleton<LogReaderService>();
    services.AddSingleton<ServiceManagerService>();

    // 注册 ViewModel
    services.AddTransient<MainWindowViewModel>();
    services.AddTransient<ServiceCreateViewModel>();

    var serviceProvider = services.BuildServiceProvider();

    // 设置主窗口
    var mainWindow = new MainWindow();
    mainWindow.DataContext = serviceProvider.GetRequiredService<MainWindowViewModel>();
    mainWindow.Show();
}
```

## 8. MVVM 最佳实践

1. **使用 CommunityToolkit.Mvvm**:
   - 使用 `[ObservableProperty]` 自动生成属性
   - 使用 `[RelayCommand]` 自动生成命令
   - 使用 `[NotifyCanExecuteChangedFor]` 更新命令状态

2. **异步操作**:
   - 所有耗时操作使用异步方法
   - 使用 `ExecuteBusyActionAsync` 管理加载状态

3. **数据绑定**:
   - 使用 `OneWay` 或 `OneTime` 绑定优化性能
   - 对于大型集合考虑使用虚拟化

4. **命令实现**:
   - 命令方法应简洁，主要逻辑放在服务层
   - 使用 `CanExecute` 控制按钮状态

5. **错误处理**:
   - ViewModel 层捕获异常并显示用户友好的错误信息
   - 详细日志记录在服务层

6. **资源管理**:
   - 实现 `IDisposable` 清理 Timer 等资源
   - 使用弱事件避免内存泄漏