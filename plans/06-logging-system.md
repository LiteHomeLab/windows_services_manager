# 日志系统设计

## 系统概述

日志系统是 WinServiceManager 的重要组成部分，负责读取和显示由 WinSW 管理的服务日志。系统需要能够实时读取正在写入的日志文件，处理大文件，并提供良好的用户体验。

## 1. LogReaderService - 日志读取服务

### 类定义
```csharp
// File: Services/LogReaderService.cs
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using WinServiceManager.Models;

namespace WinServiceManager.Services
{
    /// <summary>
    /// 日志读取服务
    /// </summary>
    public class LogReaderService : IDisposable
    {
        private readonly ILogger<LogReaderService> _logger;
        private readonly Dictionary<string, FileSystemWatcher> _watchers;
        private readonly Dictionary<string, List<Action<string>>> _subscribers;
        private readonly Timer _cleanupTimer;

        public LogReaderService(ILogger<LogReaderService> logger)
        {
            _logger = logger;
            _watchers = new Dictionary<string, FileSystemWatcher>();
            _subscribers = new Dictionary<string, List<Action<string>>>();

            // 定期清理未使用的监视器
            _cleanupTimer = new Timer(CleanupUnusedWatchers, null,
                TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
        }

        /// <summary>
        /// 读取日志文件的最后 N 行
        /// </summary>
        /// <param name="filePath">日志文件路径</param>
        /// <param name="lineCount">要读取的行数</param>
        /// <param name="encoding">文件编码（默认为 UTF-8）</param>
        /// <returns>日志行数组</returns>
        public async Task<string[]> ReadLastLinesAsync(
            string filePath,
            int lineCount,
            Encoding? encoding = null)
        {
            if (!File.Exists(filePath))
            {
                _logger.LogWarning($"Log file not found: {filePath}");
                return new string[] { $"日志文件不存在: {filePath}" };
            }

            encoding ??= Encoding.UTF8;

            try
            {
                return await Task.Run(() =>
                {
                    var lines = new List<string>();
                    var bufferSize = 1024 * 64; // 64KB buffer

                    using (var stream = new FileStream(
                        filePath,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.ReadWrite))
                    using (var reader = new StreamReader(stream, encoding))
                    {
                        var buffer = new char[bufferSize];
                        var lineBuilder = new StringBuilder();
                        var lineCountFound = 0;

                        // 从文件末尾开始读取
                        stream.Seek(0, SeekOrigin.End);
                        var fileLength = stream.Position;

                        // 如果文件太小，直接读取全部内容
                        if (fileLength < bufferSize)
                        {
                            stream.Seek(0, SeekOrigin.Begin);
                            var allText = reader.ReadToEnd();
                            var allLines = allText.Split('\n', '\r')
                                .Where(line => !string.IsNullOrWhiteSpace(line))
                                .ToArray();

                            return allLines.TakeLast(lineCount).ToArray();
                        }

                        // 分块向后读取
                        var position = fileLength;
                        var step = bufferSize;

                        while (position > 0 && lineCountFound < lineCount)
                        {
                            position = Math.Max(0, position - step);
                            stream.Seek(position, SeekOrigin.Begin);

                            var charsRead = reader.Read(buffer, 0, bufferSize);
                            var chunk = new string(buffer, 0, charsRead);

                            // 处理可能的换行符截断
                            if (position > 0)
                            {
                                var firstNewLineIndex = chunk.IndexOf('\n');
                                if (firstNewLineIndex >= 0)
                                {
                                    chunk = chunk.Substring(firstNewLineIndex + 1);
                                }
                            }

                            var chunkLines = chunk.Split('\n', '\r')
                                .Where(line => !string.IsNullOrWhiteSpace(line))
                                .Reverse();

                            foreach (var line in chunkLines)
                            {
                                lines.Insert(0, line);
                                lineCountFound++;

                                if (lineCountFound >= lineCount)
                                    break;
                            }
                        }
                    }

                    return lines.Take(lineCount).ToArray();
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error reading log file: {filePath}");
                return new string[] { $"读取日志文件失败: {ex.Message}" };
            }
        }

        /// <summary>
        /// 获取日志文件大小
        /// </summary>
        public long GetLogFileSize(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    var fileInfo = new FileInfo(filePath);
                    return fileInfo.Length;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting file size: {filePath}");
            }
            return 0;
        }

        /// <summary>
        /// 获取日志文件信息
        /// </summary>
        public async Task<LogFileInfo> GetLogFileInfoAsync(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    return new LogFileInfo
                    {
                        Exists = false,
                        FilePath = filePath
                    };
                }

                var fileInfo = new FileInfo(filePath);
                var lines = await ReadLastLinesAsync(filePath, 1);
                var lastLine = lines.FirstOrDefault();

                return new LogFileInfo
                {
                    Exists = true,
                    FilePath = filePath,
                    Size = fileInfo.Length,
                    LastModified = fileInfo.LastWriteTime,
                    LastLine = lastLine
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting log file info: {filePath}");
                return new LogFileInfo
                {
                    Exists = false,
                    FilePath = filePath,
                    Error = ex.Message
                };
            }
        }

        /// <summary>
        /// 订阅日志文件变更通知
        /// </summary>
        public void SubscribeToFileChanges(string filePath, Action<string> onNewLine)
        {
            if (!_subscribers.ContainsKey(filePath))
            {
                _subscribers[filePath] = new List<Action<string>>();
            }

            _subscribers[filePath].Add(onNewLine);

            // 创建文件监视器
            CreateFileWatcher(filePath);
        }

        /// <summary>
        /// 取消订阅日志文件变更通知
        /// </summary>
        public void UnsubscribeFromFileChanges(string filePath, Action<string>? onNewLine = null)
        {
            if (_subscribers.ContainsKey(filePath))
            {
                if (onNewLine != null)
                {
                    _subscribers[filePath].Remove(onNewLine);
                }
                else
                {
                    _subscribers[filePath].Clear();
                }

                // 如果没有订阅者了，移除监视器
                if (_subscribers[filePath].Count == 0)
                {
                    RemoveFileWatcher(filePath);
                    _subscribers.Remove(filePath);
                }
            }
        }

        /// <summary>
        /// 监控新日志行
        /// </summary>
        public async Task MonitorNewLinesAsync(string filePath, CancellationToken cancellationToken = default)
        {
            if (!File.Exists(filePath))
            {
                _logger.LogWarning($"Log file not found: {filePath}");
                return;
            }

            var lastPosition = new FileInfo(filePath).Length;

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var currentSize = GetLogFileSize(filePath);

                    if (currentSize > lastPosition)
                    {
                        // 文件有新内容
                        var newLines = await ReadNewLinesAsync(filePath, lastPosition);
                        lastPosition = currentSize;

                        // 通知订阅者
                        if (_subscribers.ContainsKey(filePath))
                        {
                            foreach (var line in newLines)
                            {
                                foreach (var subscriber in _subscribers[filePath])
                                {
                                    try
                                    {
                                        subscriber(line);
                                    }
                                    catch (Exception ex)
                                    {
                                        _logger.LogError(ex, "Error notifying log subscriber");
                                    }
                                }
                            }
                        }
                    }
                    else if (currentSize < lastPosition)
                    {
                        // 文件被截断或重新创建
                        lastPosition = 0;
                    }

                    await Task.Delay(500, cancellationToken); // 每500ms检查一次
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error monitoring log file: {filePath}");
                    await Task.Delay(5000, cancellationToken); // 出错时等待5秒
                }
            }
        }

        /// <summary>
        /// 清空日志文件
        /// </summary>
        public async Task<bool> ClearLogFileAsync(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    await File.WriteAllTextAsync(filePath, string.Empty);
                    _logger.LogInformation($"Log file cleared: {filePath}");
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error clearing log file: {filePath}");
                return false;
            }
        }

        #region Private Methods

        private async Task<string[]> ReadNewLinesAsync(string filePath, long startPosition)
        {
            return await Task.Run(() =>
            {
                var lines = new List<string>();

                try
                {
                    using var stream = new FileStream(
                        filePath,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.ReadWrite);

                    using var reader = new StreamReader(stream, Encoding.UTF8);

                    stream.Seek(startPosition, SeekOrigin.Begin);

                    while (!reader.EndOfStream)
                    {
                        var line = reader.ReadLine();
                        if (line != null)
                        {
                            lines.Add(line);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error reading new lines from: {filePath}");
                }

                return lines.ToArray();
            });
        }

        private void CreateFileWatcher(string filePath)
        {
            if (_watchers.ContainsKey(filePath))
            {
                return; // 已经存在监视器
            }

            try
            {
                var directory = Path.GetDirectoryName(filePath);
                var fileName = Path.GetFileName(filePath);

                var watcher = new FileSystemWatcher(directory!, fileName)
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime,
                    EnableRaisingEvents = true
                };

                watcher.Changed += (sender, e) =>
                {
                    // 延迟处理，避免多个事件触发
                    Task.Delay(100).ContinueWith(_ =>
                    {
                        _ = MonitorNewLinesAsync(filePath);
                    });
                };

                _watchers[filePath] = watcher;
                _logger.LogDebug($"Created file watcher for: {filePath}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creating file watcher for: {filePath}");
            }
        }

        private void RemoveFileWatcher(string filePath)
        {
            if (_watchers.ContainsKey(filePath))
            {
                var watcher = _watchers[filePath];
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
                _watchers.Remove(filePath);
                _logger.LogDebug($"Removed file watcher for: {filePath}");
            }
        }

        private void CleanupUnusedWatchers(object? state)
        {
            var filesToRemove = _watchers.Keys
                .Where(key => !_subscribers.ContainsKey(key) ||
                              _subscribers[key].Count == 0)
                .ToList();

            foreach (var file in filesToRemove)
            {
                RemoveFileWatcher(file);
            }
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            _cleanupTimer?.Dispose();

            foreach (var watcher in _watchers.Values)
            {
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
            }

            _watchers.Clear();
            _subscribers.Clear();
        }

        #endregion
    }

    /// <summary>
    /// 日志文件信息
    /// </summary>
    public class LogFileInfo
    {
        public bool Exists { get; set; }
        public string FilePath { get; set; } = string.Empty;
        public long Size { get; set; }
        public DateTime LastModified { get; set; }
        public string? LastLine { get; set; }
        public string? Error { get; set; }
    }
}
```

## 2. 日志监控 ViewModel

### LogMonitorViewModel 类
```csharp
// File: ViewModels/LogMonitorViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using WinServiceManager.Services;

namespace WinServiceManager.ViewModels
{
    /// <summary>
    /// 日志监控视图模型
    /// </summary>
    public partial class LogMonitorViewModel : ObservableObject, IDisposable
    {
        private readonly LogReaderService _logReaderService;
        private readonly string _filePath;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private Task? _monitorTask;

        [ObservableProperty]
        private ObservableCollection<string> _logLines;

        [ObservableProperty]
        private bool _isMonitoring;

        [ObservableProperty]
        private int _maxLines = 1000;

        [ObservableProperty]
        private bool _autoScroll = true;

        private readonly object _lockObject = new object();

        public LogMonitorViewModel(LogReaderService logReaderService, string filePath)
        {
            _logReaderService = logReaderService;
            _filePath = filePath;
            _cancellationTokenSource = new CancellationTokenSource();
            _logLines = new ObservableCollection<string>();

            IsMonitoring = true;
            StartMonitoring();
        }

        #region Commands

        [RelayCommand]
        private void StartMonitoring()
        {
            if (!IsMonitoring)
            {
                IsMonitoring = true;
                StartMonitoringTask();
            }
        }

        [RelayCommand]
        private void StopMonitoring()
        {
            if (IsMonitoring)
            {
                IsMonitoring = false;
                _cancellationTokenSource.Cancel();
            }
        }

        [RelayCommand]
        private void ClearLogs()
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                _logLines.Clear();
            });
        }

        [RelayCommand]
        private async Task LoadInitialLogsAsync()
        {
            try
            {
                var lines = await _logReaderService.ReadLastLinesAsync(_filePath, MaxLines);

                App.Current.Dispatcher.Invoke(() =>
                {
                    _logLines.Clear();
                    foreach (var line in lines)
                    {
                        _logLines.Add(line);
                    }
                });
            }
            catch (Exception ex)
            {
                App.Current.Dispatcher.Invoke(() =>
                {
                    _logLines.Clear();
                    _logLines.Add($"加载日志失败: {ex.Message}");
                });
            }
        }

        [RelayCommand]
        private async Task SaveLogsAsync()
        {
            var saveDialog = new SaveFileDialog
            {
                Filter = "文本文件 (*.txt)|*.txt|所有文件 (*.*)|*.*",
                FileName = $"log_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
            };

            if (saveDialog.ShowDialog() == true)
            {
                try
                {
                    await File.WriteAllLinesAsync(saveDialog.FileName, _logLines);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"保存日志失败: {ex.Message}", "错误",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        #endregion

        #region Private Methods

        private void StartMonitoringTask()
        {
            if (_monitorTask != null)
            {
                _cancellationTokenSource.Cancel();
                _monitorTask.Wait();
            }

            _cancellationTokenSource.Cancel(); // 重置之前的 token
            _cancellationTokenSource.Dispose();
            var newCts = new CancellationTokenSource();

            // 更新字段（反射方式，因为只读字段）
            typeof(LogMonitorViewModel)
                .GetField("_cancellationTokenSource", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(this, newCts);

            _monitorTask = MonitorLogAsync(newCts.Token);
        }

        private async Task MonitorLogAsync(CancellationToken cancellationToken)
        {
            // 订阅文件变更
            _logReaderService.SubscribeToFileChanges(_filePath, OnNewLine);

            try
            {
                // 先加载初始日志
                await LoadInitialLogsAsync();

                // 持续监控新日志
                await _logReaderService.MonitorNewLinesAsync(_filePath, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // 正常取消，不需要处理
            }
            catch (Exception ex)
            {
                App.Current.Dispatcher.Invoke(() =>
                {
                    _logLines.Add($"监控日志出错: {ex.Message}");
                });
            }
        }

        private void OnNewLine(string newLine)
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                lock (_lockObject)
                {
                    _logLines.Add(newLine);

                    // 限制最大行数
                    while (_logLines.Count > MaxLines)
                    {
                        _logLines.RemoveAt(0);
                    }

                    // 触发滚动事件
                    if (AutoScroll)
                    {
                        ScrollToBottomRequested?.Invoke(this, EventArgs.Empty);
                    }
                }
            });
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
            _cancellationTokenSource.Cancel();
            _monitorTask?.Wait(TimeSpan.FromSeconds(5));

            _logReaderService.UnsubscribeFromFileChanges(_filePath);
            _cancellationTokenSource.Dispose();
        }

        #endregion
    }
}
```

## 3. 日志查看器 UI 集成

### 增强的日志查看窗口
```xml
<!-- LogViewerWindow.xaml 增强版 -->
<Window x:Class="WinServiceManager.Views.LogViewerWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="日志查看器"
        Height="700" Width="1000">

    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
            <RowDefinition Height="Auto"/>
        </Grid.RowDefinitions>

        <!-- 工具栏 -->
        <Border Grid.Row="0" Background="LightGray" Padding="10">
            <Grid>
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="Auto"/>
                    <ColumnDefinition Width="20"/>
                    <ColumnDefinition Width="Auto"/>
                    <ColumnDefinition Width="*"/>
                    <ColumnDefinition Width="Auto"/>
                </Grid.ColumnDefinitions>

                <!-- 日志文件信息 -->
                <StackPanel Grid.Column="0" Orientation="Horizontal">
                    <TextBlock Text="文件大小: "/>
                    <TextBlock Text="{Binding LogFileSize, Converter={StaticResource FileSizeConverter}}"
                               FontWeight="Bold"/>
                    <TextBlock Text=" | 最后修改: "/>
                    <TextBlock Text="{Binding LastModified}"
                               FontWeight="Bold"/>
                </StackPanel>

                <!-- 日志类型选择 -->
                <StackPanel Grid.Column="2" Orientation="Horizontal">
                    <RadioButton Content="输出日志" IsChecked="True" Margin="0,0,10,0"/>
                    <RadioButton Content="错误日志"/>
                </StackPanel>

                <!-- 控制按钮 -->
                <StackPanel Grid.Column="4" Orientation="Horizontal">
                    <ToggleButton Content="监控"
                                  IsChecked="{Binding IsMonitoring}"
                                  Width="60"
                                  Margin="0,0,5,0"/>
                    <CheckBox Content="自动滚动"
                              IsChecked="{Binding AutoScroll}"
                              VerticalAlignment="Center"
                              Margin="0,0,10,0"/>
                    <Button Content="清空"
                            Command="{Binding ClearLogsCommand}"
                            Padding="10,5"
                            Margin="0,0,5,0"/>
                    <Button Content="保存"
                            Command="{Binding SaveLogsCommand}"
                            Padding="10,5"/>
                </StackPanel>
            </Grid>
        </Border>

        <!-- 日志内容 -->
        <Grid Grid.Row="1">
            <Grid.RowDefinitions>
                <RowDefinition Height="*"/>
                <RowDefinition Height="5"/>
                <RowDefinition Height="Auto"/>
            </Grid.RowDefinitions>

            <!-- 日志显示 -->
            <ScrollViewer Name="LogScrollViewer"
                          Grid.Row="0"
                          VerticalScrollBarVisibility="Auto"
                          HorizontalScrollBarVisibility="Auto"
                          Padding="10">
                <ItemsControl ItemsSource="{Binding LogLines}">
                    <ItemsControl.ItemTemplate>
                        <DataTemplate>
                            <TextBlock Text="{Binding}"
                                       FontFamily="Consolas"
                                       FontSize="12"
                                       TextWrapping="NoWrap">
                                <TextBlock.Style>
                                    <Style TargetType="TextBlock">
                                        <Style.Triggers>
                                            <DataTrigger Binding="{Binding}" Value="">
                                                <Setter Property="Visibility" Value="Collapsed"/>
                                            </DataTrigger>
                                            <DataTrigger Binding="{Binding}" Value="{x:Null}">
                                                <Setter Property="Visibility" Value="Collapsed"/>
                                            </DataTrigger>
                                        </Style.Triggers>
                                    </Style>
                                </TextBlock.Style>
                            </TextBlock>
                        </DataTemplate>
                    </ItemsControl.ItemTemplate>
                </ItemsControl>
            </ScrollViewer>

            <!-- 分隔符 -->
            <GridSplitter Grid.Row="1"
                         HorizontalAlignment="Stretch"
                         VerticalAlignment="Stretch"
                         Background="Transparent"/>

            <!-- 搜索/过滤区域 -->
            <Border Grid.Row="2"
                    Background="WhiteSmoke"
                    Padding="10"
                    BorderThickness="0,1,0,0"
                    BorderBrush="Gray">
                <Grid>
                    <Grid.ColumnDefinitions>
                        <ColumnDefinition Width="Auto"/>
                        <ColumnDefinition Width="*"/>
                        <ColumnDefinition Width="Auto"/>
                    </Grid.ColumnDefinitions>

                    <TextBlock Grid.Column="0"
                               Text="搜索:"
                               VerticalAlignment="Center"
                               Margin="0,0,10,0"/>

                    <TextBox Grid.Column="1"
                             Text="{Binding SearchText, UpdateSourceTrigger=PropertyChanged}"
                             Margin="0,0,10,0"/>

                    <Button Grid.Column="2"
                            Content="应用过滤"
                            Command="{Binding ApplyFilterCommand}"
                            Padding="10,5"/>
                </Grid>
            </Border>
        </Grid>

        <!-- 状态栏 -->
        <Border Grid.Row="2"
                Background="LightGray"
                Padding="10,5"
                BorderThickness="0,1,0,0"
                BorderBrush="Gray">
            <StackPanel Orientation="Horizontal">
                <TextBlock Text="{Binding LogLines.Count, StringFormat='行数: {0}'}"/>
                <Separator Width="20" Margin="10,0"/>
                <TextBlock Text="最大行数:"/>
                <Button Content="-"
                        Command="{Binding DecreaseMaxLinesCommand}"
                        Width="20"
                        Height="20"
                        Margin="5,0"/>
                <TextBlock Text="{Binding MaxLines}"
                           MinWidth="30"
                           TextAlignment="Center"/>
                <Button Content="+"
                        Command="{Binding IncreaseMaxLinesCommand}"
                        Width="20"
                        Height="20"
                        Margin="5,0"/>
            </StackPanel>
        </Border>
    </Grid>
</Window>
```

## 4. 性能优化策略

### 1. 大文件处理
- 使用流式读取，避免一次性加载整个文件
- 实现反向读取，只读取最后的 N 行
- 使用缓冲区优化 I/O 性能

### 2. 实时监控优化
- 使用 FileSystemWatcher 监控文件变更
- 设置合理的检查间隔（500ms）
- 批量处理多个日志行

### 3. UI 优化
- 虚拟化长列表
- 限制显示的最大行数
- 使用异步更新避免 UI 冻结

## 5. 日志轮转处理

### WinSW 日志配置
```xml
<service>
  ...
  <log mode="roll-by-size">
    <sizeThreshold>10240</sizeThreshold>  <!-- 10KB -->
    <keepFiles>8</keepFiles>
  </log>
</service>
```

### 日志文件管理
- 监控日志文件大小
- 识别日志轮转（文件名变化）
- 保留历史日志访问能力

## 6. 错误处理

### 常见错误场景
1. 文件被其他进程锁定
2. 文件不存在
3. 权限不足
4. 磁盘空间不足
5. 编码问题

### 错误处理策略
- 捕获并记录所有异常
- 显示用户友好的错误信息
- 自动重试机制
- 降级处理（如显示缓存日志）

## 7. 日志分析功能

### 日志级别识别
```csharp
public enum LogLevel
{
    Trace,
    Debug,
    Info,
    Warning,
    Error,
    Fatal
}

public LogLevel DetectLogLevel(string line)
{
    if (line.Contains("[ERROR]") || line.Contains("ERROR:"))
        return LogLevel.Error;
    if (line.Contains("[WARN]") || line.Contains("WARN:"))
        return LogLevel.Warning;
    if (line.Contains("[INFO]") || line.Contains("INFO:"))
        return LogLevel.Info;
    if (line.Contains("[DEBUG]") || line.Contains("DEBUG:"))
        return LogLevel.Debug;

    return LogLevel.Info; // 默认级别
}
```

### 日志统计
- 统计各级别日志数量
- 计算错误率
- 识别最频繁的错误

## 8. 使用示例

### 在 ViewModel 中使用
```csharp
public class ServiceLogViewModel : ObservableObject
{
    private readonly LogReaderService _logReaderService;
    private LogMonitorViewModel? _outputLogMonitor;
    private LogMonitorViewModel? _errorLogMonitor;

    public LogMonitorViewModel OutputLogMonitor =>
        _outputLogMonitor ??= new LogMonitorViewModel(_logReaderService, _outputLogPath);

    public LogMonitorViewModel ErrorLogMonitor =>
        _errorLogMonitor ??= new LogMonitorViewModel(_logReaderService, _errorLogPath);
}
```

### 在窗口中集成
```csharp
public partial class LogViewerWindow : Window
{
    public LogViewerWindow(ServiceItemViewModel service, LogReaderService logReader)
    {
        InitializeComponent();

        var viewModel = new ServiceLogViewModel(logReader, service);
        DataContext = viewModel;

        // 订阅滚动事件
        viewModel.OutputLogMonitor.ScrollToBottomRequested += (s, e) =>
        {
            OutputLogScrollViewer.ScrollToEnd();
        };
    }
}
```