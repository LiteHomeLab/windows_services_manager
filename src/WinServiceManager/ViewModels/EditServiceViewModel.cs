using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using WinServiceManager.Models;
using WinServiceManager.Services;

namespace WinServiceManager.ViewModels
{
    /// <summary>
    /// 编辑服务的视图模型
    /// </summary>
    public partial class EditServiceViewModel : BaseViewModel
    {
        private readonly ILogger<EditServiceViewModel> _logger;
        private readonly ServiceManagerService _serviceManager;
        private readonly ServiceItem _originalService;
        private readonly ServiceDependencyValidator _dependencyValidator;
        private readonly string _originalId;

        private string _displayName = string.Empty;
        private string? _description = "Managed by WinServiceManager";
        private string _executablePath = string.Empty;
        private string? _scriptPath;
        private string _arguments = string.Empty;
        private string _workingDirectory = string.Empty;
        private bool _isBusy;
        private string _errorMessage = string.Empty;
        private string _busyMessage = string.Empty;
        private bool _showPreview;
        private List<ServiceItem> _availableServices = new();

        // 依赖管理相关属性
        private List<string> _selectedDependencies = new();
        private Dictionary<string, string> _environmentVariables = new();
        private string? _serviceAccount;
        private string _startMode = "Automatic";
        private int _stopTimeout = 15000;
        private int _restartExitCode = 99;
        private bool _enableRestartOnExit = false;

        public EditServiceViewModel(
            ServiceItem service,
            ServiceManagerService serviceManager,
            ServiceDependencyValidator dependencyValidator,
            ILogger<EditServiceViewModel> logger)
        {
            _originalService = service ?? throw new ArgumentNullException(nameof(service));
            _serviceManager = serviceManager ?? throw new ArgumentNullException(nameof(serviceManager));
            _dependencyValidator = dependencyValidator ?? throw new ArgumentNullException(nameof(dependencyValidator));
            _logger = logger;
            _originalId = service.Id;

            // 初始化属性从现有服务
            _displayName = service.DisplayName;
            _description = service.Description;
            _executablePath = service.ExecutablePath;
            _scriptPath = service.ScriptPath;
            _arguments = service.Arguments;
            _workingDirectory = service.WorkingDirectory;
            _selectedDependencies = service.Dependencies?.ToList() ?? new List<string>();
            _environmentVariables = service.EnvironmentVariables?.ToDictionary(e => e.Key, e => e.Value) ?? new Dictionary<string, string>();
            _serviceAccount = service.ServiceAccount;
            _startMode = service.StartMode.ToString();
            _stopTimeout = service.StopTimeout;
            _enableRestartOnExit = service.EnableRestartOnExit;
            _restartExitCode = service.RestartExitCode;

            _logger.LogInformation("编辑服务对话框已打开: ServiceId={ServiceId}, DisplayName={DisplayName}", _originalId, _displayName);

            // 异步加载可用服务列表
            _ = LoadAvailableServicesAsync();
        }

        #region Properties

        /// <summary>
        /// 服务ID（不可编辑）
        /// </summary>
        public string ServiceId => _originalId;

        /// <summary>
        /// 服务显示名称
        /// </summary>
        public string DisplayName
        {
            get => _displayName;
            set
            {
                if (SetProperty(ref _displayName, value))
                {
                    ValidateProperty();
                    OnPropertyChanged(nameof(CanSave));
                }
            }
        }

        /// <summary>
        /// 服务描述
        /// </summary>
        public string? Description
        {
            get => _description;
            set => SetProperty(ref _description, value);
        }

        /// <summary>
        /// 可执行文件路径
        /// </summary>
        public string ExecutablePath
        {
            get => _executablePath;
            set
            {
                if (SetProperty(ref _executablePath, value))
                {
                    // 如果选择的是解释器（如 python.exe），自动启用脚本文件选项
                    if (IsInterpreter(value))
                    {
                        OnPropertyChanged(nameof(IsScriptFileEnabled));
                    }

                    // 自动设置工作目录为可执行文件所在目录
                    if (!string.IsNullOrEmpty(value))
                    {
                        string? dir = Path.GetDirectoryName(value);
                        if (!string.IsNullOrEmpty(dir))
                        {
                            WorkingDirectory = dir;
                        }
                    }

                    ValidateProperty();
                    OnPropertyChanged(nameof(CanSave));
                }
            }
        }

        /// <summary>
        /// 脚本文件路径
        /// </summary>
        public string? ScriptPath
        {
            get => _scriptPath;
            set
            {
                if (SetProperty(ref _scriptPath, value))
                {
                    ValidateProperty();
                    OnPropertyChanged(nameof(CanSave));
                }
            }
        }

        /// <summary>
        /// 启动参数
        /// </summary>
        public string Arguments
        {
            get => _arguments;
            set
            {
                if (SetProperty(ref _arguments, value))
                {
                    ValidateProperty();
                    OnPropertyChanged(nameof(CanSave));
                }
            }
        }

        /// <summary>
        /// 工作目录
        /// </summary>
        public string WorkingDirectory
        {
            get => _workingDirectory;
            set
            {
                if (SetProperty(ref _workingDirectory, value))
                {
                    ValidateProperty();
                    OnPropertyChanged(nameof(CanSave));
                }
            }
        }

        /// <summary>
        /// 是否脚本文件输入框可用
        /// </summary>
        public bool IsScriptFileEnabled => IsInterpreter(ExecutablePath);

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
                    OnPropertyChanged(nameof(CanSave));
                }
            }
        }

        /// <summary>
        /// 错误信息
        /// </summary>
        public string ErrorMessage
        {
            get => _errorMessage;
            private set => SetProperty(ref _errorMessage, value);
        }

        /// <summary>
        /// 忙状态消息
        /// </summary>
        public string BusyMessage
        {
            get => _busyMessage;
            private set => SetProperty(ref _busyMessage, value);
        }

        /// <summary>
        /// 是否显示配置预览
        /// </summary>
        public bool ShowPreview
        {
            get => _showPreview;
            private set => SetProperty(ref _showPreview, value);
        }

        /// <summary>
        /// 配置预览内容
        /// </summary>
        public string ConfigPreview { get; private set; } = string.Empty;

        /// <summary>
        /// 可用服务列表（用于依赖选择）
        /// </summary>
        public List<ServiceItem> AvailableServices
        {
            get => _availableServices;
            private set => SetProperty(ref _availableServices, value);
        }

        /// <summary>
        /// 选中的依赖服务列表
        /// </summary>
        public List<string> SelectedDependencies
        {
            get => _selectedDependencies;
            set => SetProperty(ref _selectedDependencies, value);
        }

        /// <summary>
        /// 环境变量字典
        /// </summary>
        public Dictionary<string, string> EnvironmentVariables
        {
            get => _environmentVariables;
            set => SetProperty(ref _environmentVariables, value);
        }

        /// <summary>
        /// 服务账户
        /// </summary>
        public string? ServiceAccount
        {
            get => _serviceAccount;
            set => SetProperty(ref _serviceAccount, value);
        }

        /// <summary>
        /// 启动模式
        /// </summary>
        public string StartMode
        {
            get => _startMode;
            set => SetProperty(ref _startMode, value);
        }

        /// <summary>
        /// 停止超时时间（毫秒）
        /// </summary>
        public int StopTimeout
        {
            get => _stopTimeout;
            set => SetProperty(ref _stopTimeout, value);
        }

        /// <summary>
        /// 启用退出码自动重启
        /// </summary>
        public bool EnableRestartOnExit
        {
            get => _enableRestartOnExit;
            set => SetProperty(ref _enableRestartOnExit, value);
        }

        /// <summary>
        /// 触发重启的退出码
        /// </summary>
        public int RestartExitCode
        {
            get => _restartExitCode;
            set => SetProperty(ref _restartExitCode, value);
        }

        /// <summary>
        /// 是否选择了依赖服务
        /// </summary>
        public bool HasDependencies => SelectedDependencies.Any();

        /// <summary>
        /// 是否可以保存服务（仅检查忙碌状态，验证在点击时进行）
        /// </summary>
        public bool CanSave
        {
            get
            {
                var canSave = !IsBusy;
                _logger.LogDebug("CanSave 被调用: ServiceId={ServiceId}, IsBusy={IsBusy}, CanSave={CanSave}", _originalId, IsBusy, canSave);
                return canSave;
            }
        }

        #endregion

        #region Commands

        [RelayCommand]
        private void BrowseExecutable()
        {
            var dialog = new OpenFileDialog
            {
                Title = "选择可执行文件",
                Filter = "可执行文件 (*.exe)|*.exe|所有文件 (*.*)|*.*",
                FilterIndex = 0
            };

            if (dialog.ShowDialog() == true)
            {
                ExecutablePath = dialog.FileName;
            }
        }

        [RelayCommand]
        private void BrowseScript()
        {
            var dialog = new OpenFileDialog
            {
                Title = "选择脚本文件",
                Filter = "Python脚本 (*.py)|*.py|批处理文件 (*.bat)|*.bat|PowerShell脚本 (*.ps1)|*.ps1|Node.js脚本 (*.js)|*.js|所有文件 (*.*)|*.*",
                FilterIndex = 0
            };

            if (dialog.ShowDialog() == true)
            {
                ScriptPath = dialog.FileName;
            }
        }

        [RelayCommand]
        private void BrowseWorkingDirectory()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "选择工作目录",
                Filter = "所有文件|*.*",
                CheckFileExists = false,
                CheckPathExists = true,
                FileName = "选择文件夹"
            };

            if (dialog.ShowDialog() == true)
            {
                // 获取选择的目录路径
                string? selectedPath = Path.GetDirectoryName(dialog.FileName);
                if (!string.IsNullOrEmpty(selectedPath))
                {
                    WorkingDirectory = selectedPath;
                }
            }
        }

        [RelayCommand]
        private void PreviewConfig()
        {
            if (!IsValid())
            {
                ErrorMessage = "请先填写所有必填字段";
                return;
            }

            try
            {
                var service = new ServiceItem
                {
                    Id = _originalId,
                    DisplayName = DisplayName,
                    Description = Description ?? "Managed by WinServiceManager",
                    ExecutablePath = ExecutablePath,
                    ScriptPath = ScriptPath,
                    Arguments = Arguments,
                    WorkingDirectory = WorkingDirectory,
                    Dependencies = SelectedDependencies,
                    EnvironmentVariables = EnvironmentVariables,
                    ServiceAccount = ServiceAccount,
                    StartMode = ParseStartMode(StartMode),
                    StopTimeout = StopTimeout,
                    EnableRestartOnExit = EnableRestartOnExit,
                    RestartExitCode = RestartExitCode
                };

                ConfigPreview = service.GenerateWinSWConfig();
                ShowPreview = true;
                ErrorMessage = string.Empty;
            }
            catch (Exception ex)
            {
                ErrorMessage = $"生成配置预览失败: {ex.Message}";
                ShowPreview = false;
            }
        }

        [RelayCommand(CanExecute = nameof(CanSave))]
        private async Task SaveAsync()
        {
            _logger.LogInformation("开始保存服务编辑: ServiceId={ServiceId}, DisplayName={DisplayName}", _originalId, DisplayName);

            bool isValid;
            try
            {
                isValid = IsValid();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "IsValid 方法抛出异常: ServiceId={ServiceId}", _originalId);
                ErrorMessage = $"验证时发生异常: {ex.Message}";
                return;
            }

            if (!isValid)
            {
                _logger.LogWarning("服务编辑验证失败: ServiceId={ServiceId}, Error={Error}", _originalId, ErrorMessage);
                ErrorMessage = "请检查输入，确保所有必填字段都已正确填写";
                return;
            }

            _logger.LogInformation("服务编辑验证通过: ServiceId={ServiceId}", _originalId);

            // 验证依赖关系
            if (!await ValidateDependenciesAsync())
            {
                _logger.LogWarning("服务依赖验证失败: ServiceId={ServiceId}", _originalId);
                return;
            }

            try
            {
                IsBusy = true;
                ErrorMessage = string.Empty;

                var updateRequest = new ServiceUpdateRequest
                {
                    Id = _originalId,
                    DisplayName = DisplayName,
                    Description = Description,
                    ExecutablePath = ExecutablePath,
                    ScriptPath = ScriptPath,
                    Arguments = Arguments,
                    WorkingDirectory = WorkingDirectory,
                    Dependencies = SelectedDependencies,
                    EnvironmentVariables = EnvironmentVariables,
                    ServiceAccount = ServiceAccount,
                    StartMode = StartMode,
                    StopTimeout = StopTimeout,
                    EnableRestartOnExit = EnableRestartOnExit,
                    RestartExitCode = RestartExitCode
                };

                _logger.LogInformation("构建更新请求: ServiceId={ServiceId}, DisplayName={DisplayName}, ExecutablePath={ExecutablePath}, Arguments={Arguments}, WorkingDirectory={WorkingDirectory}",
                    _originalId, DisplayName, ExecutablePath, Arguments, WorkingDirectory);

                // 注意：不在保存时对启动参数进行 SanitizeInput，因为它会把包含双引号的合法参数清空
                // 参数已经在 IsValid() 方法中验证过了，只需要检查明显的命令注入模式

                // 虽然ScriptPath是文件路径，但我们仍然需要清理路径
                if (!string.IsNullOrEmpty(updateRequest.ScriptPath))
                {
                    updateRequest.ScriptPath = Path.GetFullPath(updateRequest.ScriptPath);
                }

                BusyMessage = "正在更新服务配置...";

                _logger.LogInformation("正在调用 ServiceManager.UpdateServiceAsync: ServiceId={ServiceId}", _originalId);

                var result = await _serviceManager.UpdateServiceAsync(updateRequest);

                _logger.LogInformation("ServiceManager.UpdateServiceAsync 返回: ServiceId={ServiceId}, Success={Success}, ErrorMessage={ErrorMessage}",
                    _originalId, result.Success, result.ErrorMessage ?? "(null)");

                if (result.Success)
                {
                    BusyMessage = string.Empty;
                    IsBusy = false;

                    _logger.LogInformation("服务配置更新成功: ServiceId={ServiceId}, DisplayName={DisplayName}", _originalId, DisplayName);

                    MessageBox.Show(
                        $"服务 '{DisplayName}' 配置更新成功！\n\n注意：如果服务正在运行，需要重启服务才能应用新配置。",
                        "更新成功",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    // 关闭对话框
                    RequestClose?.Invoke();
                }
                else
                {
                    _logger.LogError("服务配置更新失败: ServiceId={ServiceId}, ErrorMessage={ErrorMessage}", _originalId, result.ErrorMessage);
                    ErrorMessage = $"更新服务配置失败: {result.ErrorMessage}";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "保存服务编辑时发生异常: ServiceId={ServiceId}", _originalId);
                ErrorMessage = $"更新服务配置时发生错误: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
                BusyMessage = string.Empty;
                _logger.LogInformation("保存服务编辑结束: ServiceId={ServiceId}", _originalId);
            }
        }

        [RelayCommand]
        private void SuggestServiceAccount()
        {
            // 根据可执行文件路径建议合适的服务账户
            if (string.IsNullOrEmpty(ExecutablePath))
            {
                MessageBox.Show("请先选择可执行文件", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                var fileName = System.IO.Path.GetFileName(ExecutablePath)?.ToLowerInvariant();

                // 根据不同的应用类型建议不同的服务账户
                string suggestedAccount = "NT SERVICE\\LocalService"; // 默认建议

                if (!string.IsNullOrEmpty(fileName))
                {
                    if (fileName.Contains("iis") || fileName.Contains("apache") || fileName.Contains("nginx") ||
                        fileName.Contains("sql") || fileName.Contains("mysql") || fileName.Contains("oracle"))
                    {
                        suggestedAccount = "NT SERVICE\\NetworkService";
                    }
                    else if (fileName.Contains("system") || fileName.Contains("admin"))
                    {
                        suggestedAccount = "NT AUTHORITY\\LocalSystem";
                    }
                }

                ServiceAccount = suggestedAccount;

                MessageBox.Show(
                    $"已建议服务账户：{suggestedAccount}\n\n" +
                    "常用服务账户说明：\n" +
                    "• LocalSystem：最高权限，有安全风险\n" +
                    "• NetworkService：网络权限，适合需要网络访问的服务\n" +
                    "• LocalService：受限权限，适合本地服务",
                    "服务账户建议",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"建议服务账户时发生错误: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void AddCommonEnvironmentVariables()
        {
            // 添加常用的环境变量
            var commonVariables = new Dictionary<string, string>
            {
                { "PATH", System.Environment.GetEnvironmentVariable("PATH") ?? "" },
                { "TEMP", System.IO.Path.GetTempPath() },
                { "TMP", System.IO.Path.GetTempPath() },
                { "COMPUTERNAME", System.Environment.MachineName },
                { "USERNAME", System.Environment.UserName }
            };

            foreach (var variable in commonVariables)
            {
                if (!string.IsNullOrEmpty(variable.Value) && !EnvironmentVariables.ContainsKey(variable.Key))
                {
                    EnvironmentVariables[variable.Key] = variable.Value;
                }
            }

            OnPropertyChanged(nameof(EnvironmentVariables));
        }

        [RelayCommand]
        private void ClearEnvironmentVariables()
        {
            var result = MessageBox.Show(
                "确定要清空所有环境变量吗？",
                "确认清空",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                EnvironmentVariables.Clear();
                OnPropertyChanged(nameof(EnvironmentVariables));
            }
        }

        [RelayCommand]
        private void RemoveEnvironmentVariable(string key)
        {
            if (!string.IsNullOrEmpty(key) && EnvironmentVariables.ContainsKey(key))
            {
                EnvironmentVariables.Remove(key);
                OnPropertyChanged(nameof(EnvironmentVariables));
            }
        }

        [RelayCommand]
        private void Cancel()
        {
            RequestClose?.Invoke();
        }

        #endregion

        #region Events

        /// <summary>
        /// 请求关闭对话框事件
        /// </summary>
        public event Action? RequestClose;

        #endregion

        #region Private Methods

        /// <summary>
        /// 加载可用服务列表
        /// </summary>
        private async Task LoadAvailableServicesAsync()
        {
            try
            {
                var services = await _serviceManager.GetAllServicesAsync();
                // 排除当前正在编辑的服务（避免形成自依赖）
                AvailableServices = services.Where(s => s.Status != ServiceStatus.NotInstalled && s.Id != _originalId).ToList();

                // 初始化已选中的依赖服务
                foreach (var service in AvailableServices)
                {
                    // 根据SelectedDependencies设置IsSelected
                    service.IsSelected = SelectedDependencies.Contains(service.Id);

                    service.PropertyChanged += (sender, e) =>
                    {
                        if (e.PropertyName == nameof(ServiceItem.IsSelected))
                        {
                            UpdateSelectedDependencies();
                        }
                    };
                }
            }
            catch
            {
                // 忽略加载错误，不会影响服务编辑
                AvailableServices = new List<ServiceItem>();
            }
        }

        /// <summary>
        /// 根据服务的选中状态更新SelectedDependencies列表
        /// </summary>
        private void UpdateSelectedDependencies()
        {
            var selectedIds = AvailableServices
                .Where(s => s.IsSelected)
                .Select(s => s.Id)
                .ToList();

            if (!selectedIds.SequenceEqual(SelectedDependencies))
            {
                SelectedDependencies = selectedIds;
                OnPropertyChanged(nameof(HasDependencies));
                OnPropertyChanged(nameof(SelectedDependencies));
            }
        }

        /// <summary>
        /// 验证依赖关系
        /// </summary>
        private async Task<bool> ValidateDependenciesAsync()
        {
            if (!SelectedDependencies.Any())
                return true;

            try
            {
                // 创建临时服务项进行验证
                var tempService = new ServiceItem
                {
                    Id = _originalId,
                    DisplayName = DisplayName,
                    Dependencies = SelectedDependencies
                };

                var result = await _dependencyValidator.ValidateDependenciesAsync(tempService, AvailableServices);

                if (!result.IsValid)
                {
                    ErrorMessage = result.GetErrorSummary();
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                ErrorMessage = $"依赖验证失败: {ex.Message}";
                return false;
            }
        }

        /// <summary>
        /// 判断是否为解释器程序
        /// </summary>
        private static bool IsInterpreter(string? executablePath)
        {
            if (string.IsNullOrEmpty(executablePath))
                return false;

            var fileName = Path.GetFileName(executablePath)?.ToLowerInvariant();
            return fileName switch
            {
                "python.exe" or "python3.exe" or "pythonw.exe" => true,
                "node.exe" => true,
                "java.exe" or "javaw.exe" => true,
                "ruby.exe" => true,
                "perl.exe" => true,
                "php.exe" => true,
                "powershell.exe" or "pwsh.exe" => true,
                "cmd.exe" => true,
                _ => false
            };
        }

        /// <summary>
        /// 解析启动模式字符串为枚举值
        /// </summary>
        private static ServiceStartupMode ParseStartMode(string? startMode)
        {
            if (string.IsNullOrEmpty(startMode))
                return ServiceStartupMode.Automatic;

            return startMode.ToLowerInvariant() switch
            {
                "automatic" => ServiceStartupMode.Automatic,
                "manual" => ServiceStartupMode.Manual,
                "disabled" => ServiceStartupMode.Disabled,
                _ => ServiceStartupMode.Automatic
            };
        }

        /// <summary>
        /// 验证输入
        /// </summary>
        private bool IsValid()
        {
            _logger.LogInformation("开始验证服务输入: ServiceId={ServiceId}", _originalId);
            var errors = new List<string>();

            // 验证服务名称
            _logger.LogInformation("验证服务名称: DisplayName='{DisplayName}', Length={Length}", DisplayName, DisplayName?.Length ?? 0);
            if (string.IsNullOrWhiteSpace(DisplayName))
            {
                errors.Add("服务名称不能为空");
            }
            else if (DisplayName.Length < 3 || DisplayName.Length > 100)
            {
                errors.Add("服务名称长度必须在3-100个字符之间");
            }

            // 验证可执行文件
            var exeExists = !string.IsNullOrWhiteSpace(ExecutablePath) && File.Exists(ExecutablePath);
            _logger.LogInformation("验证可执行文件: ExecutablePath='{ExecutablePath}', OriginalPath='{OriginalPath}', PathChanged={PathChanged}, Exists={Exists}",
                ExecutablePath, _originalService.ExecutablePath, ExecutablePath != _originalService.ExecutablePath, exeExists);
            if (string.IsNullOrWhiteSpace(ExecutablePath))
            {
                errors.Add("请选择可执行文件");
            }
            // 编辑模式：只在路径被修改时才检查文件是否存在
            else if (ExecutablePath != _originalService.ExecutablePath && !File.Exists(ExecutablePath))
            {
                errors.Add("指定的可执行文件不存在");
            }
            else
            {
                _logger.LogInformation("开始调用 PathValidator.IsValidPath 检查可执行文件");
                var pathValid = PathValidator.IsValidPath(ExecutablePath);
                _logger.LogInformation("PathValidator.IsValidPath(可执行文件) 返回: {IsValid}", pathValid);
                if (!pathValid)
                {
                    errors.Add("可执行文件路径包含非法字符");
                }
            }

            // 验证脚本文件（如果已填写）
            _logger.LogInformation("验证脚本文件: ScriptPath='{ScriptPath}', OriginalPath='{OriginalPath}'",
                ScriptPath ?? "(null)", _originalService.ScriptPath ?? "(null)");
            if (!string.IsNullOrWhiteSpace(ScriptPath))
            {
                // 编辑模式：只在路径被修改时才检查文件是否存在
                var scriptChanged = ScriptPath != _originalService.ScriptPath;
                if (scriptChanged && !File.Exists(ScriptPath))
                {
                    errors.Add("指定的脚本文件不存在");
                }
                else if (!PathValidator.IsValidPath(ScriptPath))
                {
                    errors.Add("脚本文件路径包含非法字符");
                }
            }

            // 验证工作目录
            var dirExists = !string.IsNullOrWhiteSpace(WorkingDirectory) && Directory.Exists(WorkingDirectory);
            _logger.LogInformation("验证工作目录: WorkingDirectory='{WorkingDirectory}', OriginalDir='{OriginalDir}', PathChanged={PathChanged}, Exists={Exists}",
                WorkingDirectory, _originalService.WorkingDirectory, WorkingDirectory != _originalService.WorkingDirectory, dirExists);
            if (string.IsNullOrWhiteSpace(WorkingDirectory))
            {
                errors.Add("请选择工作目录");
            }
            // 编辑模式：只在路径被修改时才检查目录是否存在
            else if (WorkingDirectory != _originalService.WorkingDirectory && !Directory.Exists(WorkingDirectory))
            {
                errors.Add("指定的工作目录不存在");
            }
            else
            {
                _logger.LogInformation("开始调用 PathValidator.IsValidPath 检查工作目录");
                var pathValid = PathValidator.IsValidPath(WorkingDirectory);
                _logger.LogInformation("PathValidator.IsValidPath 返回: {IsValid}", pathValid);
                if (!pathValid)
                {
                    errors.Add("工作目录路径包含非法字符");
                }
            }

            // 验证启动参数（仅当非空时验证）
            _logger.LogInformation("验证启动参数: Arguments='{Arguments}'", Arguments);
            // 注意：启动参数不需要严格验证，因为用户是在配置自己的服务
            // 只检查是否包含明显的命令注入模式（管道、命令连接符等）
            if (!string.IsNullOrWhiteSpace(Arguments))
            {
                // 检查明显的命令注入模式，但不检查引号（引号在参数中是合法的）
                var dangerousPatterns = new[] { "&&", "||", "| ", " |", ";", "`", "$(" };
                bool hasInjection = false;
                foreach (var pattern in dangerousPatterns)
                {
                    if (Arguments.Contains(pattern))
                    {
                        errors.Add($"启动参数包含不允许的命令模式: {pattern}");
                        hasInjection = true;
                        break;
                    }
                }
                if (!hasInjection)
                {
                    _logger.LogInformation("启动参数基本验证通过");
                }
            }

            if (errors.Any())
            {
                var errorText = string.Join("\n", errors);
                _logger.LogWarning("验证失败，错误列表: {Errors}", errorText);
                ErrorMessage = errorText;
                return false;
            }

            _logger.LogInformation("验证通过: ServiceId={ServiceId}", _originalId);
            ErrorMessage = string.Empty;
            return true;
        }

        /// <summary>
        /// 验证当前属性
        /// </summary>
        private void ValidateProperty()
        {
            // 清除错误信息
            if (ErrorMessage == "请检查输入，确保所有必填字段都已正确填写")
            {
                ErrorMessage = string.Empty;
            }

            // 如果显示预览，则重新生成
            if (ShowPreview)
            {
                PreviewConfig();
            }
        }

        #endregion
    }
}
