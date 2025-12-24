using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinServiceManager.Models;
using WinServiceManager.Services;

namespace WinServiceManager.ViewModels
{
    /// <summary>
    /// 日志查看器视图模型 - 简化版本
    /// </summary>
    public partial class LogViewerViewModel : BaseViewModel, IDisposable
    {
        private readonly LogReaderService _logReaderService;
        private readonly ServiceItem _service;
        private readonly ILogger<LogViewerViewModel> _logger;

        private string _selectedLogType = "Output";
        private ObservableCollection<string> _logLines = new();
        private bool _isMonitoring = true;
        private bool _autoScroll = true;
        private bool _isLoading;
        private string _filterText = string.Empty;
        private int _maxLines = 1000;
        private string _status = "就绪";
        private DateTime _lastUpdated = DateTime.Now;

        public LogViewerViewModel(LogReaderService logReaderService, ServiceItem service, ILogger<LogViewerViewModel> logger)
        {
            _logReaderService = logReaderService ?? throw new ArgumentNullException(nameof(logReaderService));
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // 初始化可用的日志类型
            InitializeAvailableLogTypes();

            // 启动监控
            StartMonitoring();

            // 初始加载
            _ = LoadLogsAsync();
        }

        #region Properties

        /// <summary>
        /// 窗口标题
        /// </summary>
        public string Title => $"日志查看器 - {_service.DisplayName}";

        /// <summary>
        /// 服务名称
        /// </summary>
        public string ServiceName => _service.DisplayName;

        /// <summary>
        /// 可用的日志类型列表
        /// </summary>
        public ObservableCollection<string> AvailableLogTypes { get; } = new();

        /// <summary>
        /// 当前日志文件路径
        /// </summary>
        private string CurrentLogPath => _service.FindLogPath(SelectedLogType.ToLower() switch
        {
            "output" => "out",
            "error" => "err",
            "wrapper" => "wrapper",
            _ => throw new InvalidOperationException($"Unknown log type: {SelectedLogType}")
        }) ?? string.Empty;

        /// <summary>
        /// 日志文件大小
        /// </summary>
        public long LogFileSize => _logReaderService.GetLogFileSize(CurrentLogPath);

        /// <summary>
        /// 最后修改时间
        /// </summary>
        public DateTime LastModified
        {
            get
            {
                try
                {
                    var fileInfo = new FileInfo(CurrentLogPath);
                    return fileInfo.Exists ? fileInfo.LastWriteTime : DateTime.MinValue;
                }
                catch
                {
                    return DateTime.MinValue;
                }
            }
        }

        /// <summary>
        /// 选中的日志类型
        /// </summary>
        public string SelectedLogType
        {
            get => _selectedLogType;
            set
            {
                if (SetProperty(ref _selectedLogType, value))
                {
                    // 重新订阅文件变更
                    UpdateFileSubscription();
                    _ = LoadLogsAsync();
                }
            }
        }

        /// <summary>
        /// 日志行集合
        /// </summary>
        public ObservableCollection<string> LogLines
        {
            get => _logLines;
            private set => SetProperty(ref _logLines, value);
        }

        /// <summary>
        /// 是否正在监控
        /// </summary>
        public bool IsMonitoring
        {
            get => _isMonitoring;
            private set => SetProperty(ref _isMonitoring, value);
        }

        /// <summary>
        /// 是否自动滚动
        /// </summary>
        public bool AutoScroll
        {
            get => _autoScroll;
            set => SetProperty(ref _autoScroll, value);
        }

        /// <summary>
        /// 是否正在加载
        /// </summary>
        public bool IsLoading
        {
            get => _isLoading;
            private set => SetProperty(ref _isLoading, value);
        }

        /// <summary>
        /// 过滤文本
        /// </summary>
        public string FilterText
        {
            get => _filterText;
            set
            {
                if (SetProperty(ref _filterText, value))
                {
                    ApplyFilter();
                }
            }
        }

        /// <summary>
        /// 最大行数
        /// </summary>
        public int MaxLines
        {
            get => _maxLines;
            set
            {
                if (SetProperty(ref _maxLines, Math.Max(100, value)))
                {
                    TrimLogLines();
                }
            }
        }

        /// <summary>
        /// 状态
        /// </summary>
        public string Status
        {
            get => _status;
            private set => SetProperty(ref _status, value);
        }

        /// <summary>
        /// 最后更新时间
        /// </summary>
        public DateTime LastUpdated
        {
            get => _lastUpdated;
            private set => SetProperty(ref _lastUpdated, value);
        }

        #endregion

        #region Commands

        [RelayCommand]
        private async Task RefreshAsync()
        {
            await LoadLogsAsync();
        }

        [RelayCommand]
        private async Task ClearLogsAsync()
        {
            if (MessageBox.Show(
                "确定要清空当前日志文件吗？\n\n此操作不可恢复。",
                "确认清空",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                IsLoading = true;
                Status = "正在清空日志...";

                var success = await _logReaderService.ClearLogFileAsync(CurrentLogPath);
                if (success)
                {
                    LogLines.Clear();
                    Status = "日志已清空";
                }
                else
                {
                    Status = "错误";
                    MessageBox.Show("清空日志失败", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                Status = "错误";
                MessageBox.Show($"清空日志时发生错误: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
                LastUpdated = DateTime.Now;
            }
        }

        [RelayCommand]
        private async Task SaveLogsAsync()
        {
            try
            {
                var saveDialog = new SaveFileDialog
                {
                    Title = "保存日志",
                    Filter = "文本文件 (*.txt)|*.txt|所有文件 (*.*)|*.*",
                    FileName = $"{_service.DisplayName}_{SelectedLogType}_{DateTime.Now:yyyyMMdd_HHmmss}.log"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    Status = "正在保存...";
                    await File.WriteAllLinesAsync(saveDialog.FileName, GetFilteredLines());
                    Status = "已保存";

                    MessageBox.Show(
                        $"日志已保存到:\n{saveDialog.FileName}",
                        "保存成功",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                Status = "错误";
                MessageBox.Show($"保存日志失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                Status = "就绪";
                LastUpdated = DateTime.Now;
            }
        }

        [RelayCommand]
        private void ToggleMonitoring()
        {
            IsMonitoring = !IsMonitoring;
            if (IsMonitoring)
            {
                StartMonitoring();
            }
            else
            {
                StopMonitoring();
            }
        }

        #endregion

        #region Events

        /// <summary>
        /// 滚动到底部请求事件
        /// </summary>
        public event EventHandler? ScrollToBottomRequested;

        #endregion

        #region Private Methods

        /// <summary>
        /// 初始化可用的日志类型
        /// </summary>
        private void InitializeAvailableLogTypes()
        {
            var availableLogs = _service.GetAvailableLogs();

            AvailableLogTypes.Clear();

            // 按优先级添加：Output -> Error -> Wrapper
            if (availableLogs.ContainsKey("Output"))
                AvailableLogTypes.Add("Output");

            if (availableLogs.ContainsKey("Error"))
                AvailableLogTypes.Add("Error");

            if (availableLogs.ContainsKey("Wrapper"))
                AvailableLogTypes.Add("Wrapper");

            // 设置默认选中的日志类型
            if (AvailableLogTypes.Count > 0)
            {
                SelectedLogType = AvailableLogTypes[0];
            }
        }

        /// <summary>
        /// 启动监控
        /// </summary>
        private void StartMonitoring()
        {
            IsMonitoring = true;
            UpdateFileSubscription();
            Status = "监控中";
        }

        /// <summary>
        /// 停止监控
        /// </summary>
        private void StopMonitoring()
        {
            IsMonitoring = false;
            UnsubscribeFromFile();
            Status = "已停止";
        }

        /// <summary>
        /// 更新文件订阅
        /// </summary>
        private void UpdateFileSubscription()
        {
            if (!IsMonitoring)
                return;

            // 取消旧的订阅
            UnsubscribeFromFile();

            // 订阅新的文件
            if (!string.IsNullOrEmpty(CurrentLogPath))
            {
                _logReaderService.SubscribeToFileChanges(CurrentLogPath, OnNewLogLine);
            }
        }

        /// <summary>
        /// 取消文件订阅
        /// </summary>
        private void UnsubscribeFromFile()
        {
            // 取消所有可能的日志文件订阅
            var availableLogs = _service.GetAvailableLogs();
            foreach (var logPath in availableLogs.Values)
            {
                _logReaderService.UnsubscribeFromFileChanges(logPath);
            }
        }

        /// <summary>
        /// 加载日志
        /// </summary>
        private async Task LoadLogsAsync()
        {
            try
            {
                IsLoading = true;
                Status = "正在加载...";

                var lines = await _logReaderService.ReadLastLinesAsync(CurrentLogPath, MaxLines);

                await App.Current.Dispatcher.InvokeAsync(() =>
                {
                    LogLines.Clear();
                    foreach (var line in lines)
                    {
                        LogLines.Add(line);
                    }

                    // 应用过滤器
                    ApplyFilter();

                    Status = "就绪";
                    LastUpdated = DateTime.Now;

                    // 请求滚动到底部
                    if (AutoScroll)
                    {
                        ScrollToBottomRequested?.Invoke(this, EventArgs.Empty);
                    }
                });
            }
            catch (Exception ex)
            {
                Status = "错误";
                await App.Current.Dispatcher.InvokeAsync(() =>
                {
                    LogLines.Clear();
                    LogLines.Add($"加载日志失败: {ex.Message}");
                });
            }
            finally
            {
                IsLoading = false;
                OnPropertyChanged(nameof(LogFileSize));
                OnPropertyChanged(nameof(LastModified));
                LastUpdated = DateTime.Now;
            }
        }

        /// <summary>
        /// 处理新日志行
        /// </summary>
        private void OnNewLogLine(string newLine)
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                LogLines.Add(newLine);
                TrimLogLines();
                Status = "监控中";
                LastUpdated = DateTime.Now;

                // 如果没有过滤器或新行匹配过滤器，请求滚动
                if (AutoScroll && (string.IsNullOrWhiteSpace(FilterText) ||
                    newLine.Contains(FilterText, StringComparison.OrdinalIgnoreCase)))
                {
                    ScrollToBottomRequested?.Invoke(this, EventArgs.Empty);
                }

                OnPropertyChanged(nameof(LogFileSize));
                OnPropertyChanged(nameof(LastModified));
            });
        }

        /// <summary>
        /// 应用过滤器
        /// </summary>
        private void ApplyFilter()
        {
            OnPropertyChanged(nameof(LogLines));
            LastUpdated = DateTime.Now;
        }

        /// <summary>
        /// 获取过滤后的行
        /// </summary>
        private IEnumerable<string> GetFilteredLines()
        {
            if (string.IsNullOrWhiteSpace(FilterText))
            {
                return LogLines;
            }
            return LogLines.Where(line =>
                line.Contains(FilterText, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// 修剪日志行，保持在最大行数限制内
        /// </summary>
        private void TrimLogLines()
        {
            while (LogLines.Count > MaxLines)
            {
                LogLines.RemoveAt(0);
            }
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            StopMonitoring();
        }

        #endregion
    }
}
