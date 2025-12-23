using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace WinServiceManager.ViewModels
{
    /// <summary>
    /// 设置视图模型
    /// </summary>
    public partial class SettingsViewModel : BaseViewModel
    {
        private readonly ILogger<SettingsViewModel> _logger;
        private bool _isDebugLoggingEnabled;
        private Window? _ownerWindow;
        private bool _showStartupStatusColumn = true;
        private bool _showArgumentsColumn = true;
        private bool _showStatusColumn = true;
        private bool _showDependenciesColumn = true;
        private bool _showDescriptionColumn = true;
        private bool _showExecutableColumn = true;
        private bool _showCreatedColumn = true;

        /// <summary>
        /// 日志级别选项
        /// </summary>
        public static Dictionary<string, string> LogLevelOptions { get; } = new()
        {
            { "INFO", "常规 (INFO)" },
            { "DEBUG", "调试 (DEBUG)" }
        };

        /// <summary>
        /// 是否启用调试日志
        /// </summary>
        public bool IsDebugLoggingEnabled
        {
            get => _isDebugLoggingEnabled;
            set
            {
                if (SetProperty(ref _isDebugLoggingEnabled, value))
                {
                    _logger.LogInformation("Debug logging changed to: {Enabled}", value);
                }
            }
        }

        /// <summary>
        /// 是否显示启动状态列
        /// </summary>
        public bool ShowStartupStatusColumn
        {
            get => _showStartupStatusColumn;
            set => SetProperty(ref _showStartupStatusColumn, value);
        }

        /// <summary>
        /// 是否显示启动参数列
        /// </summary>
        public bool ShowArgumentsColumn
        {
            get => _showArgumentsColumn;
            set => SetProperty(ref _showArgumentsColumn, value);
        }

        /// <summary>
        /// 是否显示状态列
        /// </summary>
        public bool ShowStatusColumn
        {
            get => _showStatusColumn;
            set => SetProperty(ref _showStatusColumn, value);
        }

        /// <summary>
        /// 是否显示依赖列
        /// </summary>
        public bool ShowDependenciesColumn
        {
            get => _showDependenciesColumn;
            set => SetProperty(ref _showDependenciesColumn, value);
        }

        /// <summary>
        /// 是否显示描述列
        /// </summary>
        public bool ShowDescriptionColumn
        {
            get => _showDescriptionColumn;
            set => SetProperty(ref _showDescriptionColumn, value);
        }

        /// <summary>
        /// 是否显示可执行文件列
        /// </summary>
        public bool ShowExecutableColumn
        {
            get => _showExecutableColumn;
            set => SetProperty(ref _showExecutableColumn, value);
        }

        /// <summary>
        /// 是否显示创建时间列
        /// </summary>
        public bool ShowCreatedColumn
        {
            get => _showCreatedColumn;
            set => SetProperty(ref _showCreatedColumn, value);
        }

        /// <summary>
        /// 保存设置命令
        /// </summary>
        public ICommand SaveSettingsCommand { get; }

        /// <summary>
        /// 取消命令
        /// </summary>
        public ICommand CancelCommand { get; }

        public SettingsViewModel(ILogger<SettingsViewModel> logger)
        {
            _logger = logger;

            // 从配置加载当前设置
            LoadCurrentSettings();

            SaveSettingsCommand = new RelayCommand(SaveSettings);
            CancelCommand = new RelayCommand(Cancel);
        }

        /// <summary>
        /// 设置所有者窗口
        /// </summary>
        public void SetOwnerWindow(Window window)
        {
            _ownerWindow = window;
        }

        /// <summary>
        /// 加载当前设置
        /// </summary>
        private void LoadCurrentSettings()
        {
            // 默认为 INFO（Debug 关闭）
            _isDebugLoggingEnabled = SettingsService.GetDebugLoggingEnabled();
            OnPropertyChanged(nameof(IsDebugLoggingEnabled));

            // 加载列显示配置
            _showStartupStatusColumn = SettingsService.GetShowStartupStatusColumn();
            _showArgumentsColumn = SettingsService.GetShowArgumentsColumn();
            _showStatusColumn = SettingsService.GetShowStatusColumn();
            _showDependenciesColumn = SettingsService.GetShowDependenciesColumn();
            _showDescriptionColumn = SettingsService.GetShowDescriptionColumn();
            _showExecutableColumn = SettingsService.GetShowExecutableColumn();
            _showCreatedColumn = SettingsService.GetShowCreatedColumn();

            OnPropertyChanged(nameof(ShowStartupStatusColumn));
            OnPropertyChanged(nameof(ShowArgumentsColumn));
            OnPropertyChanged(nameof(ShowStatusColumn));
            OnPropertyChanged(nameof(ShowDependenciesColumn));
            OnPropertyChanged(nameof(ShowDescriptionColumn));
            OnPropertyChanged(nameof(ShowExecutableColumn));
            OnPropertyChanged(nameof(ShowCreatedColumn));
        }

        /// <summary>
        /// 保存设置
        /// </summary>
        private void SaveSettings()
        {
            try
            {
                // 保存日志设置
                SettingsService.SetDebugLoggingEnabled(IsDebugLoggingEnabled);

                // 保存列显示配置
                SettingsService.SetShowStartupStatusColumn(ShowStartupStatusColumn);
                SettingsService.SetShowArgumentsColumn(ShowArgumentsColumn);
                SettingsService.SetShowStatusColumn(ShowStatusColumn);
                SettingsService.SetShowDependenciesColumn(ShowDependenciesColumn);
                SettingsService.SetShowDescriptionColumn(ShowDescriptionColumn);
                SettingsService.SetShowExecutableColumn(ShowExecutableColumn);
                SettingsService.SetShowCreatedColumn(ShowCreatedColumn);

                // 应用日志级别
                if (IsDebugLoggingEnabled)
                {
                    App.EnableDebugLogging();
                    _logger.LogInformation("Debug logging has been enabled");
                }
                else
                {
                    App.DisableDebugLogging();
                    _logger.LogInformation("Debug logging has been disabled, restored to INFO level");
                }

                // 显示成功消息
                MessageBox.Show(
                    IsDebugLoggingEnabled ? "已启用调试日志记录" : "已恢复为常规日志记录",
                    "设置已保存",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );

                // 关闭窗口
                if (_ownerWindow != null)
                {
                    _ownerWindow.DialogResult = true;
                    _ownerWindow.Close();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save settings");
                MessageBox.Show(
                    $"保存设置失败：{ex.Message}",
                    "错误",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }

        /// <summary>
        /// 取消设置
        /// </summary>
        private void Cancel()
        {
            if (_ownerWindow != null)
            {
                _ownerWindow.DialogResult = false;
                _ownerWindow.Close();
            }
        }
    }

    /// <summary>
    /// 设置服务 - 负责设置的持久化
    /// </summary>
    public static class SettingsService
    {
        private const string DebugLoggingKey = "DebugLoggingEnabled";
        private const string ShowStartupStatusColumnKey = "ShowStartupStatusColumn";
        private const string ShowArgumentsColumnKey = "ShowArgumentsColumn";
        private const string ShowStatusColumnKey = "ShowStatusColumn";
        private const string ShowDependenciesColumnKey = "ShowDependenciesColumn";
        private const string ShowDescriptionColumnKey = "ShowDescriptionColumn";
        private const string ShowExecutableColumnKey = "ShowExecutableColumn";
        private const string ShowCreatedColumnKey = "ShowCreatedColumn";

        private static readonly Dictionary<string, bool> DefaultColumnSettings = new()
        {
            { ShowStartupStatusColumnKey, true },
            { ShowArgumentsColumnKey, true },
            { ShowStatusColumnKey, true },
            { ShowDependenciesColumnKey, true },
            { ShowDescriptionColumnKey, true },
            { ShowExecutableColumnKey, true },
            { ShowCreatedColumnKey, true }
        };

        /// <summary>
        /// 获取是否启用调试日志
        /// </summary>
        public static bool GetDebugLoggingEnabled()
        {
            try
            {
                var appPath = System.IO.Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "settings.json"
                );

                if (!System.IO.File.Exists(appPath))
                    return false;

                var json = System.IO.File.ReadAllText(appPath);
                var settings = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
                if (settings != null && settings.TryGetValue(DebugLoggingKey, out var value))
                {
                    return System.Convert.ToBoolean(value);
                }
            }
            catch
            {
                // 如果读取失败，返回默认值
            }
            return false;
        }

        /// <summary>
        /// 设置是否启用调试日志
        /// </summary>
        public static void SetDebugLoggingEnabled(bool enabled)
        {
            try
            {
                var appPath = System.IO.Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "settings.json"
                );

                Dictionary<string, object> settings = new();

                // 读取现有设置
                if (System.IO.File.Exists(appPath))
                {
                    var json = System.IO.File.ReadAllText(appPath);
                    var existingSettings = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
                    if (existingSettings != null)
                    {
                        settings = existingSettings;
                    }
                }

                // 更新设置
                settings[DebugLoggingKey] = enabled;

                // 保存设置
                var jsonToWrite = Newtonsoft.Json.JsonConvert.SerializeObject(settings, Newtonsoft.Json.Formatting.Indented);
                System.IO.File.WriteAllText(appPath, jsonToWrite);
            }
            catch (Exception ex)
            {
                // 记录到系统调试输出
                System.Diagnostics.Debug.WriteLine($"Failed to save settings: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取列显示设置的通用方法
        /// </summary>
        private static bool GetColumnSetting(string key, bool defaultValue = true)
        {
            try
            {
                var appPath = System.IO.Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "settings.json"
                );

                if (!System.IO.File.Exists(appPath))
                    return defaultValue;

                var json = System.IO.File.ReadAllText(appPath);
                var settings = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
                if (settings != null && settings.TryGetValue(key, out var value))
                {
                    return System.Convert.ToBoolean(value);
                }
            }
            catch
            {
                // 如果读取失败，返回默认值
            }
            return defaultValue;
        }

        /// <summary>
        /// 设置列显示设置的通用方法
        /// </summary>
        private static void SetColumnSetting(string key, bool value)
        {
            try
            {
                var appPath = System.IO.Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "settings.json"
                );

                Dictionary<string, object> settings = new();

                // 读取现有设置
                if (System.IO.File.Exists(appPath))
                {
                    var json = System.IO.File.ReadAllText(appPath);
                    var existingSettings = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
                    if (existingSettings != null)
                    {
                        settings = existingSettings;
                    }
                }

                // 更新设置
                settings[key] = value;

                // 保存设置
                var jsonToWrite = Newtonsoft.Json.JsonConvert.SerializeObject(settings, Newtonsoft.Json.Formatting.Indented);
                System.IO.File.WriteAllText(appPath, jsonToWrite);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save column setting: {ex.Message}");
            }
        }

        // 列显示配置的 Get/Set 方法
        public static bool GetShowStartupStatusColumn() => GetColumnSetting(ShowStartupStatusColumnKey);
        public static void SetShowStartupStatusColumn(bool value) => SetColumnSetting(ShowStartupStatusColumnKey, value);

        public static bool GetShowArgumentsColumn() => GetColumnSetting(ShowArgumentsColumnKey);
        public static void SetShowArgumentsColumn(bool value) => SetColumnSetting(ShowArgumentsColumnKey, value);

        public static bool GetShowStatusColumn() => GetColumnSetting(ShowStatusColumnKey);
        public static void SetShowStatusColumn(bool value) => SetColumnSetting(ShowStatusColumnKey, value);

        public static bool GetShowDependenciesColumn() => GetColumnSetting(ShowDependenciesColumnKey);
        public static void SetShowDependenciesColumn(bool value) => SetColumnSetting(ShowDependenciesColumnKey, value);

        public static bool GetShowDescriptionColumn() => GetColumnSetting(ShowDescriptionColumnKey);
        public static void SetShowDescriptionColumn(bool value) => SetColumnSetting(ShowDescriptionColumnKey, value);

        public static bool GetShowExecutableColumn() => GetColumnSetting(ShowExecutableColumnKey);
        public static void SetShowExecutableColumn(bool value) => SetColumnSetting(ShowExecutableColumnKey, value);

        public static bool GetShowCreatedColumn() => GetColumnSetting(ShowCreatedColumnKey);
        public static void SetShowCreatedColumn(bool value) => SetColumnSetting(ShowCreatedColumnKey, value);
    }
}
