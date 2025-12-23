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
        }

        /// <summary>
        /// 保存设置
        /// </summary>
        private void SaveSettings()
        {
            try
            {
                // 保存设置
                SettingsService.SetDebugLoggingEnabled(IsDebugLoggingEnabled);

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
    }
}
