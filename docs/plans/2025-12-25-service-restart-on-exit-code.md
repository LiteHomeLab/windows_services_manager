# 服务退出码自动重启功能实施计划

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**目标:** 让被 WinServiceManager 管理的程序能够通过特定退出码(99)触发自动重启，而正常退出(0)或手动停止时不重启。

**架构:** 在 ServiceItem 模型中添加退出码重启配置属性，并在生成 WinSW XML 配置时添加 `<onfailure>` 元素。WinSW 能自动区分手动停止和程序主动退出，因此只有在程序主动返回指定退出码时才触发重启。

**技术栈:** C# / .NET 8, WinSW v3.0+, XDocument (XML 生成)

---

## 背景知识

### WinSW 的 `<onfailure>` 行为

当通过 WinServiceManager 手动停止服务时：
1. WinSW 向子进程发送 `SERVICE_CONTROL_STOP` 信号
2. WinSW 知道这是它主动发起的停止，`<onfailure>` 不会触发

当程序自己退出时：
1. WinSW 检测到进程退出
2. 如果退出码匹配 `restartExitCode`，`<onfailure>` 触发重启

### 退出码语义

| 退出码 | 场景 | 是否重启 |
|--------|------|----------|
| 0 | 程序正常退出 / 手动停止 | 否 |
| 99 | 程序请求重启 | 是 |
| 其他 | 错误退出 | 否（可配置） |

### 支持的语言

- **C#:** `Environment.Exit(99);`
- **Python:** `sys.exit(99)`
- **Node.js:** `process.exit(99)`
- **批处理:** `exit /b 99`

---

## Task 1: 在 ServiceItem 添加退出码重启配置属性

**Files:**
- Modify: `src/WinServiceManager/Models/ServiceItem.cs`

**Step 1: 添加新属性到 ServiceItem 类**

在 `ServiceItem.cs` 中约第 204 行（LastStartupTime 之后）添加以下属性：

```csharp
/// <summary>
/// 启用退出码自动重启功能
/// 当程序返回指定的退出码时，WinSW 会自动重启程序
/// </summary>
public bool EnableRestartOnExit { get; set; } = false;

/// <summary>
/// 触发重启的退出码（默认 99）
/// 程序返回此退出码时，WinSW 会自动重启程序
/// 返回 0 或其他退出码时，不会重启
/// </summary>
public int RestartExitCode { get; set; } = 99;
```

**Step 2: 验证编译**

```bash
dotnet build src/WinServiceManager.sln
```

Expected: 编译成功，无错误

**Step 3: 提交**

```bash
git add src/WinServiceManager/Models/ServiceItem.cs
git commit -m "feat(restart): add EnableRestartOnExit and RestartExitCode properties"
```

---

## Task 2: 修改 GenerateWinSWConfig 方法生成 onfailure 元素

**Files:**
- Modify: `src/WinServiceManager/Models/ServiceItem.cs`

**Step 1: 修改 GenerateWinSWConfig 方法**

在 `GenerateWinSWConfig()` 方法中，约第 329 行（`<stoptimeout>` 之后）添加 `<onfailure>` 元素生成逻辑：

找到这段代码：
```csharp
// 添加停止超时
serviceElement.Add(new XElement("stoptimeout", StopTimeout));
```

在其后添加：
```csharp
// 添加退出码自动重启配置
if (EnableRestartOnExit)
{
    var onfailureElement = new XElement("onfailure",
        new XElement("restart",
            new XAttribute("restartExitCode", RestartExitCode)
        )
    );
    serviceElement.Add(onfailureElement);
}
```

**Step 2: 验证生成的 XML**

运行程序并创建一个启用了退出码重启的服务，检查生成的配置文件：

```bash
# 启动程序创建测试服务
dotnet run --project src/WinServiceManager

# 在 GUI 中创建服务并勾选"启用退出码重启"
# 然后查看生成的配置文件
```

生成的 XML 应包含：
```xml
<service>
  <!-- ... 其他配置 ... -->
  <stoptimeout>15000</stoptimeout>
  <onfailure>
    <restart restartExitCode="99" />
  </onfailure>
</service>
```

**Step 3: 提交**

```bash
git add src/WinServiceManager/Models/ServiceItem.cs
git commit -m "feat(restart): add onfailure element generation for exit code restart"
```

---

## Task 3: 添加 CreateServiceViewModel 的退出码重启属性

**Files:**
- Modify: `src/WinServiceManager/ViewModels/CreateServiceViewModel.cs`

**Step 1: 添加私有字段**

在 CreateServiceViewModel.cs 中约第 55 行（StopTimeout 之后）添加私有字段：

```csharp
private int _restartExitCode = 99;
private bool _enableRestartOnExit = false;
```

**Step 2: 添加公共属性**

在 CreateServiceViewModel.cs 中约第 323 行（StopTimeout 属性之后）添加公共属性：

```csharp
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
```

**Step 3: 修改 CreateAsync 方法传递新属性**

在 CreateServiceViewModel.cs 的 CreateAsync 方法中约第 446 行，修改 ServiceCreateRequest 的创建：

找到：
```csharp
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
```

修改为：
```csharp
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
    StopTimeout = StopTimeout,
    EnableRestartOnExit = EnableRestartOnExit,
    RestartExitCode = RestartExitCode
};
```

**Step 4: 验证编译**

```bash
dotnet build src/WinServiceManager.sln
```

Expected: 编译成功

**Step 5: 提交**

```bash
git add src/WinServiceManager/ViewModels/CreateServiceViewModel.cs
git commit -m "feat(restart): add EnableRestartOnExit properties to CreateServiceViewModel"
```

---

## Task 4: 添加 EditServiceViewModel 的退出码重启属性

**Files:**
- Modify: `src/WinServiceManager/ViewModels/EditServiceViewModel.cs`

**Step 1: 添加私有字段**

在 EditServiceViewModel.cs 中约第 44 行（StopTimeout 之后）添加私有字段：

```csharp
private int _restartExitCode = 99;
private bool _enableRestartOnExit = false;
```

**Step 2: 在构造函数中初始化字段**

在 EditServiceViewModel.cs 的构造函数中约第 69 行（_stopTimeout 初始化之后）添加：

```csharp
_enableRestartOnExit = service.EnableRestartOnExit;
_restartExitCode = service.RestartExitCode;
```

**Step 3: 添加公共属性**

在 EditServiceViewModel.cs 中约第 293 行（StopTimeout 属性之后）添加公共属性：

```csharp
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
```

**Step 4: 修改 PreviewConfig 方法**

在 EditServiceViewModel.cs 的 PreviewConfig 方法中约第 383 行，修改 ServiceItem 的创建：

找到：
```csharp
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
    StopTimeout = StopTimeout
};
```

修改为：
```csharp
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
```

**Step 5: 修改 SaveAsync 方法**

在 EditServiceViewModel.cs 的 SaveAsync 方法中约第 448 行，修改 ServiceUpdateRequest 的创建：

找到：
```csharp
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
    StopTimeout = StopTimeout
};
```

修改为：
```csharp
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
```

**Step 6: 验证编译**

```bash
dotnet build src/WinServiceManager.sln
```

Expected: 编译成功

**Step 7: 提交**

```bash
git add src/WinServiceManager/ViewModels/EditServiceViewModel.cs
git commit -m "feat(restart): add EnableRestartOnExit properties to EditServiceViewModel"
```

---

## Task 5: 添加 ServiceCreateRequest 和 ServiceUpdateRequest 的属性支持

**Files:**
- Modify: `src/WinServiceManager/Models/ServiceCreateRequest.cs`
- Modify: `src/WinServiceManager/Models/ServiceUpdateRequest.cs`

**Step 1: 在 ServiceCreateRequest 添加属性**

在 ServiceCreateRequest.cs 中约第 78 行（StopTimeout 之后）添加：

```csharp
/// <summary>
/// 启用退出码自动重启
/// </summary>
public bool EnableRestartOnExit { get; set; } = false;

/// <summary>
/// 触发重启的退出码
/// </summary>
public int RestartExitCode { get; set; } = 99;
```

**Step 2: 在 ServiceUpdateRequest 添加属性**

在 ServiceUpdateRequest.cs 中约第 79 行（StopTimeout 之后）添加：

```csharp
/// <summary>
/// 启用退出码自动重启
/// </summary>
public bool EnableRestartOnExit { get; set; } = false;

/// <summary>
/// 触发重启的退出码
/// </summary>
public int RestartExitCode { get; set; } = 99;
```

**Step 3: 修改 ServiceManagerService.CreateServiceAsync**

在 ServiceManagerService.cs 的 CreateServiceAsync 方法中约第 63 行，修改 ServiceItem 的创建：

找到：
```csharp
var service = new ServiceItem
{
    DisplayName = request.DisplayName,
    Description = request.Description ?? "Managed by WinServiceManager",
    ExecutablePath = request.ExecutablePath,
    ScriptPath = request.ScriptPath,
    Arguments = request.Arguments,
    WorkingDirectory = request.WorkingDirectory,
    Status = ServiceStatus.Installing,
    Dependencies = request.Dependencies ?? new List<string>(),
    EnvironmentVariables = request.EnvironmentVariables ?? new Dictionary<string, string>(),
    ServiceAccount = request.ServiceAccount,
    StartMode = ParseStartMode(request.StartMode),
    StopTimeout = request.StopTimeout
};
```

修改为：
```csharp
var service = new ServiceItem
{
    DisplayName = request.DisplayName,
    Description = request.Description ?? "Managed by WinServiceManager",
    ExecutablePath = request.ExecutablePath,
    ScriptPath = request.ScriptPath,
    Arguments = request.Arguments,
    WorkingDirectory = request.WorkingDirectory,
    Status = ServiceStatus.Installing,
    Dependencies = request.Dependencies ?? new List<string>(),
    EnvironmentVariables = request.EnvironmentVariables ?? new Dictionary<string, string>(),
    ServiceAccount = request.ServiceAccount,
    StartMode = ParseStartMode(request.StartMode),
    StopTimeout = request.StopTimeout,
    EnableRestartOnExit = request.EnableRestartOnExit,
    RestartExitCode = request.RestartExitCode
};
```

**Step 4: 修改 ServiceManagerService.UpdateServiceAsync**

在 ServiceManagerService.cs 的 UpdateServiceAsync 方法中约第 302 行，修改属性更新：

找到：
```csharp
existingService.ServiceAccount = request.ServiceAccount;
existingService.StartMode = ParseStartMode(request.StartMode);
existingService.StopTimeout = request.StopTimeout;
existingService.UpdatedAt = DateTime.Now;
```

修改为：
```csharp
existingService.ServiceAccount = request.ServiceAccount;
existingService.StartMode = ParseStartMode(request.StartMode);
existingService.StopTimeout = request.StopTimeout;
existingService.EnableRestartOnExit = request.EnableRestartOnExit;
existingService.RestartExitCode = request.RestartExitCode;
existingService.UpdatedAt = DateTime.Now;
```

**Step 5: 验证编译**

```bash
dotnet build src/WinServiceManager.sln
```

Expected: 编译成功

**Step 6: 提交**

```bash
git add src/WinServiceManager/Models/ServiceCreateRequest.cs src/WinServiceManager/Models/ServiceUpdateRequest.cs src/WinServiceManager/Services/ServiceManagerService.cs
git commit -m "feat(restart): add EnableRestartOnExit to request models and ServiceManagerService"
```

---

## Task 6: 添加 UI 控件（创建服务对话框）

**Files:**
- Modify: `src/WinServiceManager/Views/CreateServiceDialog.xaml`

**Step 1: 在"服务账户配置" GroupBox 中添加退出码重启选项**

在 CreateServiceDialog.xaml 中约第 312 行（服务选项之后）添加：

找到：
```xml
<!-- 服务选项 -->
<StackPanel Grid.Row="6" Grid.Column="0" Grid.ColumnSpan="3" Orientation="Horizontal">
    <CheckBox Content="创建后自动启动服务"
              IsChecked="{Binding AutoStart}"
              IsEnabled="{Binding IsBusy, Converter={StaticResource InverseBooleanConverter}}"
              Margin="0,5,15,5"/>
    <CheckBox Content="服务失败时自动重启"
              IsChecked="{Binding AutoRestart}"
              IsEnabled="{Binding IsBusy, Converter={StaticResource InverseBooleanConverter}}"
              Margin="0,5"/>
</StackPanel>
```

在其后添加新的行和控件：
```xml
<!-- 退出码重启配置 -->
<StackPanel Grid.Row="7" Grid.Column="0" Grid.ColumnSpan="3" Orientation="Horizontal" Margin="0,10,0,0">
    <CheckBox Content="启用退出码自动重启"
              IsChecked="{Binding EnableRestartOnExit}"
              IsEnabled="{Binding IsBusy, Converter={StaticResource InverseBooleanConverter}}"
              Margin="0,5,15,5"
              ToolTip="当程序返回指定的退出码时，WinSW 会自动重启程序"/>
    <TextBlock Text="退出码:" VerticalAlignment="Center" Margin="0,5,5,5"/>
    <TextBox Width="80"
             Text="{Binding RestartExitCode, UpdateSourceTrigger=PropertyChanged}"
             IsEnabled="{Binding EnableRestartOnExit}"
             Padding="5"
             IsReadOnly="{Binding IsBusy}"
             ToolTip="程序返回此退出码时触发重启（默认 99）"/>
    <TextBlock Text="（程序返回此退出码时将触发重启）" VerticalAlignment="Center" Foreground="#666" FontSize="11" Margin="8,5,0,5"/>
</StackPanel>
```

同时需要在 Grid.RowDefinitions 中添加新的行定义。找到：
```xml
<Grid.RowDefinitions>
    <RowDefinition Height="Auto"/>
    <RowDefinition Height="12"/>
    <RowDefinition Height="Auto"/>
    <RowDefinition Height="12"/>
    <RowDefinition Height="Auto"/>
    <RowDefinition Height="12"/>
    <RowDefinition Height="Auto"/>
</Grid.RowDefinitions>
```

修改为：
```xml
<Grid.RowDefinitions>
    <RowDefinition Height="Auto"/>
    <RowDefinition Height="12"/>
    <RowDefinition Height="Auto"/>
    <RowDefinition Height="12"/>
    <RowDefinition Height="Auto"/>
    <RowDefinition Height="12"/>
    <RowDefinition Height="Auto"/>
    <RowDefinition Height="12"/>
    <RowDefinition Height="Auto"/>
</Grid.RowDefinitions>
```

**Step 2: 验证编译**

```bash
dotnet build src/WinServiceManager.sln
```

Expected: 编译成功，UI 显示正常

**Step 3: 提交**

```bash
git add src/WinServiceManager/Views/CreateServiceDialog.xaml
git commit -m "feat(restart): add exit code restart UI to CreateServiceDialog"
```

---

## Task 7: 添加 UI 控件（编辑服务对话框）

**Files:**
- Modify: `src/WinServiceManager/Views/EditServiceDialog.xaml`

首先读取 EditServiceDialog.xaml 以确认其结构。

**Step 1: 在"服务账户配置" GroupBox 中添加退出码重启选项**

参考 CreateServiceDialog 的修改，在 EditServiceDialog.xaml 中添加类似的 UI 控件。

**Step 2: 验证编译**

```bash
dotnet build src/WinServiceManager.sln
```

**Step 3: 提交**

```bash
git add src/WinServiceManager/Views/EditServiceDialog.xaml
git commit -m "feat(restart): add exit code restart UI to EditServiceDialog"
```

---

## Task 8: 编写单元测试

**Files:**
- Create: `src/WinServiceManager.Tests/UnitTests/ServiceItemExitCodeTests.cs`

**Step 1: 编写测试 - 验证 EnableRestartOnExit 生成正确的 XML**

```csharp
using Xunit;
using WinServiceManager.Models;

namespace WinServiceManager.Tests.UnitTests
{
    public class ServiceItemExitCodeTests
    {
        [Fact]
        public void GenerateWinSWConfig_WithEnableRestartOnExit_IncludesOnfailureElement()
        {
            // Arrange
            var service = new ServiceItem
            {
                Id = "test-service",
                DisplayName = "Test Service",
                ExecutablePath = "C:\\test\\app.exe",
                WorkingDirectory = "C:\\test",
                EnableRestartOnExit = true,
                RestartExitCode = 99
            };

            // Act
            var config = service.GenerateWinSWConfig();

            // Assert
            Assert.Contains("<onfailure>", config);
            Assert.Contains("restartExitCode=\"99\"", config);
            Assert.Contains("<restart", config);
        }

        [Fact]
        public void GenerateWinSWConfig_WithoutEnableRestartOnExit_DoesNotIncludeOnfailureElement()
        {
            // Arrange
            var service = new ServiceItem
            {
                Id = "test-service",
                DisplayName = "Test Service",
                ExecutablePath = "C:\\test\\app.exe",
                WorkingDirectory = "C:\\test",
                EnableRestartOnExit = false
            };

            // Act
            var config = service.GenerateWinSWConfig();

            // Assert
            Assert.DoesNotContain("<onfailure>", config);
        }

        [Fact]
        public void GenerateWinSWConfig_WithCustomExitCode_UsesCorrectExitCode()
        {
            // Arrange
            var service = new ServiceItem
            {
                Id = "test-service",
                DisplayName = "Test Service",
                ExecutablePath = "C:\\test\\app.exe",
                WorkingDirectory = "C:\\test",
                EnableRestartOnExit = true,
                RestartExitCode = 42
            };

            // Act
            var config = service.GenerateWinSWConfig();

            // Assert
            Assert.Contains("restartExitCode=\"42\"", config);
        }
    }
}
```

**Step 2: 运行测试验证**

```bash
dotnet test src/WinServiceManager.Tests/UnitTests/ServiceItemExitCodeTests.cs -v n
```

Expected: 测试通过

**Step 3: 提交**

```bash
git add src/WinServiceManager.Tests/UnitTests/ServiceItemExitCodeTests.cs
git commit -m "test(restart): add unit tests for exit code restart feature"
```

---

## Task 9: 手动测试完整流程

**Step 1: 创建测试程序**

创建一个简单的测试程序 `TestRestartApp.exe` 或 Python 脚本：

**C# 版本:**
```csharp
using System;
using System.Threading;

class Program
{
    static void Main()
    {
        Console.WriteLine($"TestRestartApp started at {DateTime.Now}");

        // 模拟程序运行，然后返回退出码 99
        Thread.Sleep(5000);

        Console.WriteLine("Exiting with code 99 to trigger restart...");
        Environment.Exit(99);
    }
}
```

**Python 版本:**
```python
import sys
import time

print(f"TestRestartApp started at {time.strftime('%Y-%m-%d %H:%M:%S')}")
time.sleep(5)
print("Exiting with code 99 to trigger restart...")
sys.exit(99)
```

**Step 2: 使用 WinServiceManager 创建服务**

1. 启动 WinServiceManager
2. 点击"创建服务"
3. 填写服务信息：
   - 服务名称: `TestRestartService`
   - 可执行文件: 选择测试程序
   - 勾选"启用退出码自动重启"
   - 退出码: `99`
4. 创建服务并启动

**Step 3: 验证行为**

1. 观察服务是否启动
2. 等待 5 秒后程序退出（返回码 99）
3. 观察服务是否自动重启
4. 检查日志文件确认重启行为

**Step 4: 测试手动停止**

1. 在 WinServiceManager 中点击"停止"
2. 确认服务停止后**没有**自动重启

**Step 5: 测试正常退出**

修改测试程序返回退出码 0，验证不会触发重启。

---

## 验证清单

完成所有任务后，确认以下功能正常工作：

- [ ] 创建服务时可以配置退出码重启选项
- [ ] 编辑服务时可以修改退出码重启选项
- [ ] 生成的 WinSW XML 配置包含正确的 `<onfailure>` 元素
- [ ] 程序返回指定退出码时触发自动重启
- [ ] 程序返回退出码 0 时不会重启
- [ ] 通过 WinServiceManager 手动停止服务时不会重启
- [ ] 单元测试全部通过

---

## 使用示例

### Python 程序实现

```python
import sys
import time
import os

def main():
    service_name = os.environ.get('SERVICE_NAME', 'MyService')
    print(f"Service {service_name} is running...")

    while True:
        # 这里监听外部命令（命名管道、HTTP、文件等）
        # 如果收到"重启"命令：
        # sys.exit(99)  # WinSW 会重启

        # 如果收到"停止"命令：
        # sys.exit(0)   # WinSW 不会重启

        time.sleep(1)

if __name__ == '__main__':
    main()
```

### C# 程序实现

```csharp
// 收到"重启"命令
Environment.Exit(99);  // WinSW 会重启

// 收到"停止"命令
Environment.Exit(0);   // WinSW 不会重启
```

### Node.js 程序实现

```javascript
// 收到"重启"命令
process.exit(99);  // WinSW 会重启

// 收到"停止"命令
process.exit(0);   // WinSW 不会重启
```<tool_call>Glob<arg_key>pattern</arg_key><arg_value>**/Views/*.xaml