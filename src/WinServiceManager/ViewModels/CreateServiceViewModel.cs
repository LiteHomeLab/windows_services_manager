using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
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
    /// 创建服务的视图模型
    /// </summary>
    public partial class CreateServiceViewModel : BaseViewModel
    {
        private readonly ServiceManagerService _serviceManager;

        private string _displayName = string.Empty;
        private string? _description = "Managed by WinServiceManager";
        private string _executablePath = string.Empty;
        private string? _scriptPath;
        private string _arguments = string.Empty;
        private string _workingDirectory = string.Empty;
        private bool _autoStart = true;
        private bool _autoRestart = true;
        private bool _isBusy;
        private string _errorMessage = string.Empty;
        private string _busyMessage = string.Empty;
        private bool _showPreview;
        private readonly ServiceDependencyValidator _dependencyValidator;
        private List<ServiceItem> _availableServices = new();

        // 依赖管理相关属性
        private List<string> _selectedDependencies = new();
        private Dictionary<string, string> _environmentVariables = new();
        private string? _serviceAccount;
        private string _startMode = "Automatic";
        private int _stopTimeout = 15000;

        public CreateServiceViewModel(
            ServiceManagerService serviceManager,
            ServiceDependencyValidator dependencyValidator)
        {
            _serviceManager = serviceManager ?? throw new ArgumentNullException(nameof(serviceManager));
            _dependencyValidator = dependencyValidator ?? throw new ArgumentNullException(nameof(dependencyValidator));

            // 异步加载可用服务列表
            _ = LoadAvailableServicesAsync();
        }

        #region Properties

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
                    OnPropertyChanged(nameof(CanCreate));
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
                    OnPropertyChanged(nameof(CanCreate));
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
                    OnPropertyChanged(nameof(CanCreate));
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
                    OnPropertyChanged(nameof(CanCreate));
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
                    OnPropertyChanged(nameof(CanCreate));
                }
            }
        }

        /// <summary>
        /// 创建后自动启动
        /// </summary>
        public bool AutoStart
        {
            get => _autoStart;
            set => SetProperty(ref _autoStart, value);
        }

        /// <summary>
        /// 自动重启
        /// </summary>
        public bool AutoRestart
        {
            get => _autoRestart;
            set => SetProperty(ref _autoRestart, value);
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
                    OnPropertyChanged(nameof(CanCreate));
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
        /// 是否选择了依赖服务
        /// </summary>
        public bool HasDependencies => SelectedDependencies.Any();

        /// <summary>
        /// 是否可以创建服务（仅检查忙碌状态，验证在点击时进行）
        /// </summary>
        public bool CanCreate => !IsBusy;

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
                    DisplayName = DisplayName,
                    Description = Description ?? "Managed by WinServiceManager",
                    ExecutablePath = ExecutablePath,
                    ScriptPath = ScriptPath,
                    Arguments = Arguments,
                    WorkingDirectory = WorkingDirectory
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

        [RelayCommand(CanExecute = nameof(CanCreate))]
        private async Task CreateAsync()
        {
            if (!IsValid())
            {
                ErrorMessage = "请检查输入，确保所有必填字段都已正确填写";
                return;
            }

            // 验证依赖关系
            if (!await ValidateDependenciesAsync())
            {
                return;
            }

            try
            {
                IsBusy = true;
                ErrorMessage = string.Empty;

                var request = new ServiceCreateRequest
                {
                    DisplayName = DisplayName,
                    Description = Description,
                    ExecutablePath = ExecutablePath,
                    ScriptPath = ScriptPath,
                    Arguments = Arguments,
                    WorkingDirectory = WorkingDirectory,
                    AutoStart = AutoStart,
                    Dependencies = SelectedDependencies,
                    EnvironmentVariables = EnvironmentVariables,
                    ServiceAccount = ServiceAccount,
                    StartMode = StartMode,
                    StopTimeout = StopTimeout
                };

                // 清理和验证参数
                request.Arguments = CommandValidator.SanitizeInput(request.Arguments);

                // 虽然ScriptPath是文件路径，但我们仍然需要清理路径
                if (!string.IsNullOrEmpty(request.ScriptPath))
                {
                    request.ScriptPath = Path.GetFullPath(request.ScriptPath);
                }

                BusyMessage = "正在创建服务...";

                var result = await _serviceManager.CreateServiceAsync(request);

                if (result.Success)
                {
                    BusyMessage = string.Empty;
                    IsBusy = false;

                    MessageBox.Show(
                        $"服务 '{DisplayName}' 创建成功！\n\n服务ID: {result.Data}",
                        "创建成功",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    // 关闭对话框
                    RequestClose?.Invoke();
                }
                else
                {
                    ErrorMessage = $"创建服务失败: {result.ErrorMessage}";
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"创建服务时发生错误: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
                BusyMessage = string.Empty;
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
                AvailableServices = services.Where(s => s.Status != ServiceStatus.NotInstalled).ToList();

                // 初始化已选中的依赖服务
                foreach (var service in AvailableServices)
                {
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
                // 忽略加载错误，不会影响服务创建
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
                    Id = Guid.NewGuid().ToString("N"), // 临时ID
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
        /// 验证输入
        /// </summary>
        private bool IsValid()
        {
            var errors = new List<string>();

            // 验证服务名称
            if (string.IsNullOrWhiteSpace(DisplayName))
            {
                errors.Add("服务名称不能为空");
            }
            else if (DisplayName.Length < 3 || DisplayName.Length > 100)
            {
                errors.Add("服务名称长度必须在3-100个字符之间");
            }

            // 验证可执行文件
            if (string.IsNullOrWhiteSpace(ExecutablePath))
            {
                errors.Add("请选择可执行文件");
            }
            else if (!File.Exists(ExecutablePath))
            {
                errors.Add("指定的可执行文件不存在");
            }
            else if (!PathValidator.IsValidPath(ExecutablePath))
            {
                errors.Add("可执行文件路径包含非法字符");
            }

            // 验证脚本文件（如果已填写）
            if (!string.IsNullOrWhiteSpace(ScriptPath))
            {
                if (!File.Exists(ScriptPath))
                {
                    errors.Add("指定的脚本文件不存在");
                }
                else if (!PathValidator.IsValidPath(ScriptPath))
                {
                    errors.Add("脚本文件路径包含非法字符");
                }
            }

            // 验证工作目录
            if (string.IsNullOrWhiteSpace(WorkingDirectory))
            {
                errors.Add("请选择工作目录");
            }
            else if (!Directory.Exists(WorkingDirectory))
            {
                errors.Add("指定的工作目录不存在");
            }
            else if (!PathValidator.IsValidPath(WorkingDirectory))
            {
                errors.Add("工作目录路径包含非法字符");
            }

            // 验证启动参数（仅当非空时验证）
            if (!string.IsNullOrWhiteSpace(Arguments) && !CommandValidator.IsValidInput(Arguments))
            {
                errors.Add("启动参数包含非法字符");
            }

            if (errors.Any())
            {
                ErrorMessage = string.Join("\n", errors);
                return false;
            }

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