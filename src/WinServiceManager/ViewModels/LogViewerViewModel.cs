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
    /// 日志查看器视图模型
    /// </summary>
    public partial class LogViewerViewModel : BaseViewModel, IDisposable
    {
        private readonly LogReaderService _logReaderService;
        private readonly ServiceItem _service;
        private readonly System.Threading.Timer _refreshTimer;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private Task? _monitorTask;

        private string _selectedLogType = "Output";
        private ObservableCollection<string> _logLines = new();
        private ObservableCollection<string> _filteredLogLines = new();
        private bool _isMonitoring = true;
        private bool _autoScroll = true;
        private bool _isLoading;
        private bool _showFilterBar;
        private string _filterText = string.Empty;
        private bool _isFilterApplied;
        private int _maxLines = 1000;
        private int _refreshInterval = 5;
        private string _status = "就绪";
        private DateTime _lastUpdated = DateTime.Now;

        public LogViewerViewModel(LogReaderService logReaderService, ServiceItem service)
        {
            _logReaderService = logReaderService ?? throw new ArgumentNullException(nameof(logReaderService));
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _cancellationTokenSource = new CancellationTokenSource();

            // 初始化定时器
            _refreshTimer = new Timer(OnRefreshTimer, null, Timeout.Infinite, Timeout.Infinite);

            // 订阅文件变更
            _logReaderService.SubscribeToFileChanges(OutputLogPath, OnNewLogLine);
            _logReaderService.SubscribeToFileChanges(ErrorLogPath, OnNewLogLine);

            // 启动监控
            StartMonitoring();

            // 初始加载
            _ = LoadInitialLogsAsync();
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
        /// 当前日志文件路径
        /// </summary>
        private string CurrentLogPath => SelectedLogType switch
        {
            "Output" => _service.OutputLogPath,
            "Error" => _service.ErrorLogPath,
            _ => _service.OutputLogPath
        };

        /// <summary>
        /// 输出日志路径
        /// </summary>
        public string OutputLogPath => _service.OutputLogPath;

        /// <summary>
        /// 错误日志路径
        /// </summary>
        public string ErrorLogPath => _service.ErrorLogPath;

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
        /// 过滤后的日志行集合
        /// </summary>
        public ObservableCollection<string> FilteredLogLines
        {
            get => _filteredLogLines;
            private set => SetProperty(ref _filteredLogLines, value);
        }

        /// <summary>
        /// 是否正在监控
        /// </summary>
        public bool IsMonitoring
        {
            get => _isMonitoring;
            private set
            {
                if (SetProperty(ref _isMonitoring, value))
                {
                    UpdateTimer();
                }
            }
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
        /// 是否显示过滤栏
        /// </summary>
        public bool ShowFilterBar
        {
            get => _showFilterBar;
            set => SetProperty(ref _showFilterBar, value);
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
        /// 是否应用了过滤器
        /// </summary>
        public bool IsFilterApplied
        {
            get => _isFilterApplied;
            private set => SetProperty(ref _isFilterApplied, value);
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
        /// 刷新间隔（秒）
        /// </summary>
        public int RefreshInterval
        {
            get => _refreshInterval;
            set
            {
                if (SetProperty(ref _refreshInterval, Math.Max(1, Math.Min(60, value))))
                {
                    UpdateTimer();
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
        private async Task ClearLogs()
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
                    FilteredLogLines.Clear();
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
                    await File.WriteAllLinesAsync(saveDialog.FileName, FilteredLogLines);
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
        private void ToggleFilterBar()
        {
            ShowFilterBar = !ShowFilterBar;
        }

        [RelayCommand]
        private void ApplyFilter()
        {
            if (!string.IsNullOrWhiteSpace(FilterText))
            {
                var filtered = LogLines.Where(line =>
                    line.Contains(FilterText, StringComparison.OrdinalIgnoreCase)).ToList();

                FilteredLogLines.Clear();
                foreach (var line in filtered)
                {
                    FilteredLogLines.Add(line);
                }

                IsFilterApplied = true;
            }
            else
            {
                ClearFilter();
            }

            OnPropertyChanged(nameof(FilteredLogLines));
            LastUpdated = DateTime.Now;
        }

        [RelayCommand]
        private void ClearFilter()
        {
            FilterText = string.Empty;
            FilteredLogLines.Clear();
            foreach (var line in LogLines)
            {
                FilteredLogLines.Add(line);
            }
            IsFilterApplied = false;
            OnPropertyChanged(nameof(FilteredLogLines));
        }

        [RelayCommand]
        private void IncreaseMaxLines()
        {
            MaxLines += 500;
        }

        [RelayCommand]
        private void DecreaseMaxLines()
        {
            MaxLines = Math.Max(100, MaxLines - 500);
        }

        [RelayCommand]
        private void ToggleMonitoring()
        {
            if (IsMonitoring)
            {
                StopMonitoring();
            }
            else
            {
                StartMonitoring();
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
        /// 启动监控
        /// </summary>
        private void StartMonitoring()
        {
            IsMonitoring = true;
            _monitorTask = MonitorLogAsync(_cancellationTokenSource.Token);
        }

        /// <summary>
        /// 停止监控
        /// </summary>
        public void StopMonitoring()
        {
            IsMonitoring = false;
            _cancellationTokenSource.Cancel();

            try
            {
                _monitorTask?.Wait(TimeSpan.FromSeconds(5));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error stopping log monitoring: {ex.Message}");
            }
        }

        /// <summary>
        /// 监控日志变更
        /// </summary>
        private async Task MonitorLogAsync(CancellationToken cancellationToken)
        {
            try
            {
                await _logReaderService.MonitorNewLinesAsync(CurrentLogPath, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // 正常取消，不需要处理
            }
            catch (Exception ex)
            {
                App.Current.Dispatcher.Invoke(() =>
                {
                    Status = "监控错误";
                    System.Diagnostics.Debug.WriteLine($"Log monitoring error: {ex.Message}");
                });
            }
        }

        /// <summary>
        /// 加载初始日志
        /// </summary>
        private async Task LoadInitialLogsAsync()
        {
            await LoadLogsAsync();
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
                App.Current.Dispatcher.Invoke(() =>
                {
                    LogLines.Clear();
                    LogLines.Add($"加载日志失败: {ex.Message}");
                    ApplyFilter();
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

                // 如果没有过滤器或新行匹配过滤器
                if (!IsFilterApplied || string.IsNullOrWhiteSpace(FilterText) ||
                    newLine.Contains(FilterText, StringComparison.OrdinalIgnoreCase))
                {
                    FilteredLogLines.Add(newLine);
                }

                // 限制最大行数
                TrimLogLines();

                // 更新状态
                Status = "监控中";
                LastUpdated = DateTime.Now;

                // 请求滚动到底部
                if (AutoScroll)
                {
                    ScrollToBottomRequested?.Invoke(this, EventArgs.Empty);
                }

                OnPropertyChanged(nameof(LogFileSize));
                OnPropertyChanged(nameof(LastModified));
            });
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

            while (FilteredLogLines.Count > MaxLines)
            {
                FilteredLogLines.RemoveAt(0);
            }
        }

        /// <summary>
        /// 更新定时器
        /// </summary>
        private void UpdateTimer()
        {
            if (IsMonitoring)
            {
                _refreshTimer.Change(TimeSpan.FromSeconds(RefreshInterval), TimeSpan.FromSeconds(RefreshInterval));
            }
            else
            {
                _refreshTimer.Change(Timeout.Infinite, Timeout.Infinite);
            }
        }

        /// <summary>
        /// 定时器回调
        /// </summary>
        private async void OnRefreshTimer(object? state)
        {
            if (!IsMonitoring || IsLoading)
                return;

            await LoadLogsAsync();
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            StopMonitoring();

            _refreshTimer?.Dispose();
            _cancellationTokenSource?.Dispose();

            _logReaderService.UnsubscribeFromFileChanges(OutputLogPath);
            _logReaderService.UnsubscribeFromFileChanges(ErrorLogPath);
        }

        #endregion
    }
}