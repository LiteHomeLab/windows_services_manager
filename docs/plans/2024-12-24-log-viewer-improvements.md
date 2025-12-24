# 日志查看器改进和服务目录访问功能实现计划

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**目标：** 改进日志查看功能，支持通过文件后缀名动态发现三种 WinSW 日志类型（*.out.log、*.err.log、*.wrapper.log），并添加"打开服务目录"按钮快速访问服务文件。

**架构：** 在 ServiceItem 模型中添加基于文件模式匹配的日志发现方法，LogViewerViewModel 通过动态查找获取日志路径，UI 层添加包装器日志选项和目录访问按钮。

**技术栈：** WPF (XAML), C# (.NET 8), CommunityToolkit.Mvvm, WinSW

---

## 关键文件路径

```
src/WinServiceManager/
├── Models/ServiceItem.cs                    # 服务数据模型，添加日志发现方法
├── ViewModels/
│   ├── LogViewerViewModel.cs               # 日志查看器 ViewModel，修改日志路径获取
│   └── MainWindowViewModel.cs              # 主窗口 ViewModel，添加目录打开命令
└── Views/
    ├── LogViewerWindow.xaml                # 日志查看器 UI，添加包装器日志选项
    └── MainWindow.xaml                      # 主窗口 UI，添加打开目录按钮
```

---

## Task 1: 在 ServiceItem 中添加动态日志发现方法

**Files:**
- Modify: `src/WinServiceManager/Models/ServiceItem.cs:253+`

**Step 1: 在 ServiceItem.cs 中添加 FindLogPath 方法**

在第 253 行（ErrorLogPath 属性之后）添加以下代码：

```csharp
/// <summary>
/// 根据后缀类型查找日志文件
/// </summary>
/// <param name="logType">日志类型: "out", "err", "wrapper"</param>
/// <returns>匹配的日志文件路径，如果不存在则返回空字符串</returns>
public string FindLogPath(string logType)
{
    try
    {
        if (!Directory.Exists(LogDirectory))
            return string.Empty;

        var pattern = $"*.{logType}.log";
        var files = Directory.GetFiles(LogDirectory, pattern);

        return files.FirstOrDefault() ?? string.Empty;
    }
    catch
    {
        return string.Empty;
    }
}

/// <summary>
/// 获取所有可用的日志文件
/// </summary>
/// <returns>日志类型到文件路径的字典</returns>
public Dictionary<string, string> GetAvailableLogs()
{
    var result = new Dictionary<string, string>();

    try
    {
        if (!Directory.Exists(LogDirectory))
            return result;

        // 查找 *.out.log
        var outLog = FindLogPath("out");
        if (!string.IsNullOrEmpty(outLog))
            result["Output"] = outLog;

        // 查找 *.err.log
        var errLog = FindLogPath("err");
        if (!string.IsNullOrEmpty(errLog))
            result["Error"] = errLog;

        // 查找 *.wrapper.log
        var wrapperLog = FindLogPath("wrapper");
        if (!string.IsNullOrEmpty(wrapperLog))
            result["Wrapper"] = wrapperLog;
    }
    catch
    {
        // 忽略异常，返回空字典
    }

    return result;
}
```

**Step 2: 验证代码编译**

Run: `dotnet build src/WinServiceManager.sln`
Expected: BUILD SUCCESS

**Step 3: 提交**

```bash
git add src/WinServiceManager/Models/ServiceItem.cs
git commit -m "feat: 添加动态日志文件发现方法

- FindLogPath(): 根据后缀模式(*.{type}.log)查找日志文件
- GetAvailableLogs(): 返回所有可用日志文件的字典"
```

---

## Task 2: 修改 LogViewerViewModel 使用动态日志发现

**Files:**
- Modify: `src/WinServiceManager/ViewModels/LogViewerViewModel.cs`

**Step 1: 添加 AvailableLogTypes 属性**

在类属性区域（约第 30 行，字段定义区域）添加：

```csharp
/// <summary>
/// 可用的日志类型列表
/// </summary>
public ObservableCollection<string> AvailableLogTypes { get; } = new();
```

**Step 2: 修改 CurrentLogPath 属性使用动态查找**

修改第 76-81 行：

**原代码：**
```csharp
private string CurrentLogPath => SelectedLogType switch
{
    "Output" => _service.OutputLogPath,
    "Error" => _service.ErrorLogPath,
    _ => _service.OutputLogPath
};
```

**修改为：**
```csharp
private string CurrentLogPath => _service.FindLogPath(SelectedLogType.ToLower());
```

**Step 3: 添加初始化可用日志类型的方法**

在私有方法区域添加：

```csharp
/// <summary>
/// 初始化可用的日志类型
/// </summary>
private async Task InitializeAvailableLogTypesAsync()
{
    try
    {
        await Task.Run(() =>
        {
            var availableLogs = _service.GetAvailableLogs();

            App.Current.Dispatcher.Invoke(() =>
            {
                AvailableLogTypes.Clear();

                // 按优先级添加：Output -> Error -> Wrapper
                if (availableLogs.ContainsKey("Output"))
                    AvailableLogTypes.Add("Output");

                if (availableLogs.ContainsKey("Error"))
                    AvailableLogTypes.Add("Error");

                if (availableLogs.ContainsKey("Wrapper"))
                    AvailableLogTypes.Add("Wrapper");

                // 如果当前选中的日志类型不可用，切换到第一个可用的
                if (!AvailableLogTypes.Contains(SelectedLogType) && AvailableLogTypes.Any())
                {
                    SelectedLogType = AvailableLogTypes.First();
                }
            });
        });
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "初始化可用日志类型失败");
    }
}
```

**Step 4: 在构造函数中调用初始化方法**

在构造函数末尾（约第 60 行之后）添加：

```csharp
// 初始化可用的日志类型
_ = InitializeAvailableLogTypesAsync();
```

**Step 5: 修改文件监控订阅为动态订阅**

修改构造函数中的订阅代码（约第 51-52 行）：

**原代码：**
```csharp
_logReaderService.SubscribeToFileChanges(OutputLogPath, OnNewLogLine);
_logReaderService.SubscribeToFileChanges(ErrorLogPath, OnNewLogLine);
```

**修改为：**
```csharp
// 根据可用的日志类型动态订阅
var availableLogs = _service.GetAvailableLogs();
foreach (var logPath in availableLogs.Values)
{
    _logReaderService.SubscribeToFileChanges(logPath, OnNewLogLine);
}
```

**Step 6: 修改 Dispose 方法中的取消订阅**

修改 Dispose 方法中的取消订阅代码（约第 97-98 行）：

**原代码：**
```csharp
_logReaderService.UnsubscribeFromFileChanges(OutputLogPath, OnNewLogLine);
_logReaderService.UnsubscribeFromFileChanges(ErrorLogPath, OnNewLogLine);
```

**修改为：**
```csharp
// 取消所有日志文件的监控
var availableLogs = _service.GetAvailableLogs();
foreach (var logPath in availableLogs.Values)
{
    _logReaderService.UnsubscribeFromFileChanges(logPath, OnNewLogLine);
}
```

**Step 7: 验证代码编译**

Run: `dotnet build src/WinServiceManager.sln`
Expected: BUILD SUCCESS

**Step 8: 提交**

```bash
git add src/WinServiceManager/ViewModels/LogViewerViewModel.cs
git commit -m "feat(log-viewer): 使用动态日志发现替代硬编码路径

- 添加 AvailableLogTypes 集合属性
- CurrentLogPath 使用 FindLogPath() 动态查找
- 文件监控订阅根据实际可用日志文件动态创建
- 添加 InitializeAvailableLogTypesAsync() 方法"
```

---

## Task 3: 在 LogViewerWindow UI 中添加包装器日志选项

**Files:**
- Modify: `src/WinServiceManager/Views/LogViewerWindow.xaml:94-102`

**Step 1: 修改日志类型选择区域**

修改第 94-102 行的日志类型选择 StackPanel：

**原代码：**
```xml
<StackPanel Grid.Column="0" Orientation="Horizontal">
    <TextBlock Text="日志类型:" VerticalAlignment="Center" Margin="0,0,10,0"/>
    <RadioButton Content="输出日志"
                 IsChecked="{Binding SelectedLogType, Converter={StaticResource StringToBooleanConverter}, ConverterParameter=Output}"
                 Margin="0,0,15,0"/>
    <RadioButton Content="错误日志"
                 IsChecked="{Binding SelectedLogType, Converter={StaticResource StringToBooleanConverter}, ConverterParameter=Error}"/>
</StackPanel>
```

**修改为：**
```xml
<StackPanel Grid.Column="0" Orientation="Horizontal">
    <TextBlock Text="日志类型:" VerticalAlignment="Center" Margin="0,0,10,0"/>
    <RadioButton Content="输出日志 (*.out.log)"
                 IsChecked="{Binding SelectedLogType, Converter={StaticResource StringToBooleanConverter}, ConverterParameter=Output}"
                 Margin="0,0,15,0"/>
    <RadioButton Content="错误日志 (*.err.log)"
                 IsChecked="{Binding SelectedLogType, Converter={StaticResource StringToBooleanConverter}, ConverterParameter=Error}"
                 Margin="0,0,15,0"/>
    <RadioButton Content="包装器日志 (*.wrapper.log)"
                 IsChecked="{Binding SelectedLogType, Converter={StaticResource StringToBooleanConverter}, ConverterParameter=Wrapper}"/>
</StackPanel>
```

**Step 2: 验证代码编译**

Run: `dotnet build src/WinServiceManager.sln`
Expected: BUILD SUCCESS

**Step 3: 提交**

```bash
git add src/WinServiceManager/Views/LogViewerWindow.xaml
git commit -m "feat(log-viewer): 添加包装器日志类型选项

- 添加 *.wrapper.log 日志类型 RadioButton
- 在日志类型标签中显示文件后缀名提示"
```

---

## Task 4: 在 MainWindowViewModel 中添加打开服务目录命令

**Files:**
- Modify: `src/WinServiceManager/ViewModels/MainWindowViewModel.cs:422+`

**Step 1: 添加 OpenSelectedServiceDirectory 命令**

在第 422 行（OpenServicesFolder 方法之后）添加：

```csharp
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
        var servicePath = SelectedService.Service.ServiceDirectory;

        if (Directory.Exists(servicePath))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"{servicePath}\"",
                UseShellExecute = true
            });
        }
        else
        {
            MessageBox.Show($"服务目录不存在:\n{servicePath}", "目录不存在", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
    catch (Exception ex)
    {
        MessageBox.Show($"无法打开服务目录: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
    }
}
```

**Step 2: 验证代码编译**

Run: `dotnet build src/WinServiceManager.sln`
Expected: BUILD SUCCESS

**Step 3: 提交**

```bash
git add src/WinServiceManager/ViewModels/MainWindowViewModel.cs
git commit -m "feat(main-window): 添加打开选中服务目录命令

- OpenSelectedServiceDirectory: 打开选中服务的 WinSW 程序目录
- 包含空选择验证和目录存在性检查
- 使用 explorer.exe 打开目录"
```

---

## Task 5: 在 MainWindow 工具栏添加打开目录按钮

**Files:**
- Modify: `src/WinServiceManager/Views/MainWindow.xaml:81+`

**Step 1: 在工具栏添加按钮**

在第 81 行（查看日志按钮之后）添加：

```xml
<Separator/>
<Button Command="{Binding OpenSelectedServiceDirectoryCommand}">
    <StackPanel Orientation="Horizontal">
        <TextBlock Text="📁" Margin="0,0,5,0"/>
        <TextBlock Text="打开目录"/>
    </StackPanel>
</Button>
```

**Step 2: 验证代码编译**

Run: `dotnet build src/WinServiceManager.sln`
Expected: BUILD SUCCESS

**Step 3: 提交**

```bash
git add src/WinServiceManager/Views/MainWindow.xaml
git commit -m "feat(main-window): 工具栏添加打开目录按钮

- 在查看日志按钮后添加打开目录按钮
- 使用文件夹图标 (📁) 保持 UI 一致性"
```

---

## Task 6: 手动测试验证

**Files:**
- No code changes

**Step 1: 运行应用程序**

Run: `dotnet run --project src/WinServiceManager`
Expected: 应用程序启动，主窗口正常显示

**Step 2: 测试打开服务目录功能**

1. 在服务列表中选中一个服务
2. 点击工具栏的"打开目录"按钮
3. 验证资源管理器打开正确的服务目录（格式：`services\{ServiceID}\`）

**Step 3: 测试未选中服务时的提示**

1. 确保没有选中任何服务（点击服务列表空白处）
2. 点击"打开目录"按钮
3. 验证显示"请先选择一个服务"提示

**Step 4: 测试日志查看器的三种日志类型**

1. 选中一个服务
2. 点击"查看日志"按钮
3. 依次点击三种日志类型选项：
   - 输出日志 (*.out.log)
   - 错误日志 (*.err.log)
   - 包装器日志 (*.wrapper.log)
4. 验证每种日志类型都能正确显示内容（如果文件存在）

**Step 5: 测试日志文件不存在的情况**

1. 找到一个只有部分日志文件的服务目录
2. 打开日志查看器
3. 验证不存在的日志类型显示空内容或友好提示

**Step 6: 测试日志实时监控**

1. 打开日志查看器并选择输出日志
2. 保持日志查看器打开
3. 重启对应的服务
4. 验证日志查看器实时显示新的日志内容

---

## 相关文档和测试

**需参考的文档：**
- `CLAUDE.md` - 项目开发规则和架构说明
- `docs/plans/` - 其他实现计划（如果有）

**现有测试文件：**
- `src/WinServiceManager.Tests/UnitTests/ViewModels/LogViewerViewModelTests.cs` - 日志查看器单元测试
- `src/WinServiceManager.Tests/UnitTests/Models/ServiceItemTests.cs` - ServiceItem 模型单元测试

**注意：** 本计划不包含单元测试的编写任务。如有需要，可以在实施完成后补充测试。

---

## 设计说明

### 动态日志发现方案的优势

1. **灵活性**：不依赖固定的文件名格式，支持 WinSW 自定义日志配置
2. **健壮性**：即使某些日志文件不存在，应用也能正常运行
3. **可扩展性**：将来可以轻松添加对其他日志类型的支持

### 日志文件匹配规则

使用 `*.{type}.log` 模式匹配：
- `*.out.log` - 输出日志
- `*.err.log` - 错误日志
- `*.wrapper.log` - WinSW 包装器日志

---

## 提交规范总结

所有提交遵循以下格式：
```
feat(scope): description

- detailed change 1
- detailed change 2
```

提交类型：
- `feat` - 新功能
- `fix` - 错误修复
- `refactor` - 重构

作用域：
- `log-viewer` - 日志查看器相关
- `main-window` - 主窗口相关
- 无作用域 - 模型层等跨组件改动
