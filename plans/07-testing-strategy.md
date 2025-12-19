# 测试策略

## 测试概述

本文档定义了 WinServiceManager 项目的测试策略，包括单元测试、集成测试、端到端测试和性能测试。测试旨在确保应用程序的可靠性、稳定性和性能。

## 1. 测试金字塔

```
        /\
       /E2E\      少量端到端测试
      /______\
     /        \
    /Integration\  适量集成测试
   /____________\
  /              \
 /   Unit Tests   \   大量单元测试
/________________\
```

## 2. 测试项目设置

### 项目结构
```
WinServiceManager/
├── src/
│   ├── WinServiceManager/
│   └── WinServiceManager.Tests/
│       ├── UnitTests/
│       │   ├── Services/
│       │   ├── ViewModels/
│       │   └── Utilities/
│       ├── IntegrationTests/
│       │   ├── ServiceManagement/
│       │   └── DataPersistence/
│       ├── E2ETests/
│       │   ├── ServiceLifecycle/
│       │   └── UIScenarios/
│       └── TestData/
│           ├── SampleScripts/
│           └── MockServices/
```

### 测试框架和工具
```xml
<!-- WinServiceManager.Tests.csproj -->
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <!-- 测试框架 -->
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.8.0" />
    <PackageReference Include="xunit" Version="2.6.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.5.3" />
    <PackageReference Include="Xunit.Extensions" Version="2.6.2" />

    <!-- Mock 框架 -->
    <PackageReference Include="Moq" Version="4.20.69" />
    <PackageReference Include="Microsoft.Extensions.Logging.Testing" Version="8.0.0" />

    <!-- 测试工具 -->
    <PackageReference Include="FluentAssertions" Version="6.12.0" />
    <PackageReference Include="coverlet.collector" Version="6.0.0">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>

    <!-- WPF 测试工具 -->
    <PackageReference Include="FluentWPF" Version="1.0.1" />
  </ItemGroup>

</Project>
```

## 3. 单元测试

### 3.1 ServiceItem 测试
```csharp
// File: UnitTests/Models/ServiceItemTests.cs
using WinServiceManager.Models;
using Xunit;

namespace WinServiceManager.Tests.Models
{
    public class ServiceItemTests
    {
        [Fact]
        public void Constructor_ShouldInitializeWithDefaultValues()
        {
            // Arrange & Act
            var service = new ServiceItem();

            // Assert
            service.Should().NotBeNull();
            service.Id.Should().NotBeEmpty();
            service.DisplayName.Should().BeEmpty();
            service.Status.Should().Be(ServiceStatus.NotInstalled);
            service.CreatedAt.Should().BeCloseTo(DateTime.Now, TimeSpan.FromSeconds(1));
        }

        [Fact]
        public void GetFullArguments_WithScriptPath_ShouldIncludeScriptFirst()
        {
            // Arrange
            var service = new ServiceItem
            {
                ExecutablePath = @"C:\Python39\python.exe",
                ScriptPath = @"D:\Scripts\main.py",
                Arguments = "--prod --verbose"
            };

            // Act
            var fullArgs = service.GetFullArguments();

            // Assert
            fullArgs.Should().Contain("\"D:\\Scripts\\main.py\"");
            fullArgs.Should().Contain("--prod");
            fullArgs.Should().Contain("--verbose");
        }

        [Fact]
        public void GetFullArguments_WithoutScriptPath_ShouldReturnArguments()
        {
            // Arrange
            var service = new ServiceItem
            {
                ExecutablePath = @"C:\MyApp\app.exe",
                Arguments = "--port 8080"
            };

            // Act
            var fullArgs = service.GetFullArguments();

            // Assert
            fullArgs.Should().Be("--port 8080");
        }

        [Fact]
        public void GenerateWinSWConfig_ShouldCreateValidXml()
        {
            // Arrange
            var service = new ServiceItem
            {
                Id = "test-service-123",
                DisplayName = "Test Service",
                Description = "A test service",
                ExecutablePath = @"C:\app.exe",
                Arguments = "--prod",
                WorkingDirectory = @"C:\app"
            };

            // Act
            var config = service.GenerateWinSWConfig();

            // Assert
            config.Should().Contain("<id>test-service-123</id>");
            config.Should().Contain("<name>Test Service</name>");
            config.Should().Contain("<executable>C:\\app.exe</executable>");
            config.Should().Contain("<arguments>--prod</arguments>");
            config.Should().Contain("<workingdirectory>C:\\app</workingdirectory>");
            config.Should().Contain("<stopparentprocessfirst>true</stopparentprocessfirst>");
        }
    }
}
```

### 3.2 ServiceManagerService 测试
```csharp
// File: UnitTests/Services/ServiceManagerServiceTests.cs
using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using WinServiceManager.Models;
using WinServiceManager.Services;
using Xunit;

namespace WinServiceManager.Tests.Services
{
    public class ServiceManagerServiceTests
    {
        private readonly Mock<WinSWWrapper> _mockWinSW;
        private readonly Mock<DataService> _mockDataService;
        private readonly ServiceManagerService _serviceManager;

        public ServiceManagerServiceTests()
        {
            _mockWinSW = new Mock<WinSWWrapper>(NullLogger<WinSWWrapper>.Instance);
            _mockDataService = new Mock<DataService>(NullLogger<DataService>.Instance);

            // Setup default mock behaviors
            _mockDataService.Setup(x => x.LoadServicesAsync())
                .ReturnsAsync(new List<ServiceItem>());

            _serviceManager = new ServiceManagerService(
                _mockWinSW.Object,
                _mockDataService.Object,
                NullLogger<ServiceManagerService>.Instance);
        }

        [Fact]
        public async Task CreateServiceAsync_ShouldCallWinSWInstall()
        {
            // Arrange
            var request = new ServiceCreateRequest
            {
                DisplayName = "Test Service",
                ExecutablePath = @"C:\app.exe",
                WorkingDirectory = @"C:\app"
            };

            _mockWinSW.Setup(x => x.InstallAsync(It.IsAny<ServiceItem>()))
                .ReturnsAsync(ServiceOperationResult.SuccessResult(ServiceOperationType.Install));

            _mockDataService.Setup(x => x.SaveServicesAsync(It.IsAny<List<ServiceItem>>()))
                .Returns(Task.CompletedTask);

            // Act
            await _serviceManager.InitializeAsync();
            var result = await _serviceManager.CreateServiceAsync(request);

            // Assert
            result.Success.Should().BeTrue();
            _mockWinSW.Verify(x => x.InstallAsync(It.Is<ServiceItem>(s =>
                s.DisplayName == "Test Service" &&
                s.ExecutablePath == @"C:\app.exe")), Times.Once);
        }

        [Fact]
        public async Task StartServiceAsync_ShouldCheckStatusBeforeStart()
        {
            // Arrange
            var service = new ServiceItem
            {
                Id = "test-123",
                DisplayName = "Test Service",
                Status = ServiceStatus.Stopped
            };

            _mockDataService.Setup(x => x.LoadServicesAsync())
                .ReturnsAsync(new List<ServiceItem> { service });

            _mockWinSW.Setup(x => x.StartAsync(service.Id))
                .ReturnsAsync(ServiceOperationResult.SuccessResult(ServiceOperationType.Start));

            _mockDataService.Setup(x => x.SaveServicesAsync(It.IsAny<List<ServiceItem>>()))
                .Returns(Task.CompletedTask);

            // Act
            await _serviceManager.InitializeAsync();
            var result = await _serviceManager.StartServiceAsync(service.Id);

            // Assert
            result.Success.Should().BeTrue();
            _mockWinSW.Verify(x => x.StartAsync(service.Id), Times.Once);
        }

        [Fact]
        public async Task StartServiceAsync_WhenAlreadyRunning_ShouldReturnFailure()
        {
            // Arrange
            var service = new ServiceItem
            {
                Id = "test-123",
                DisplayName = "Test Service",
                Status = ServiceStatus.Running
            };

            _mockDataService.Setup(x => x.LoadServicesAsync())
                .ReturnsAsync(new List<ServiceItem> { service });

            // Act
            await _serviceManager.InitializeAsync();
            var result = await _serviceManager.StartServiceAsync(service.Id);

            // Assert
            result.Success.Should().BeFalse();
            result.ErrorMessage.Should().Contain("cannot be started");
            _mockWinSW.Verify(x => x.StartAsync(It.IsAny<string>()), Times.Never);
        }
    }
}
```

### 3.3 WinSWWrapper 测试
```csharp
// File: UnitTests/Services/WinSWWrapperTests.cs
using Microsoft.Extensions.Logging.Abstractions;
using System.Diagnostics;
using WinServiceManager.Models;
using WinServiceManager.Services;
using Xunit;

namespace WinServiceManager.Tests.Services
{
    public class WinSWWrapperTests : IDisposable
    {
        private readonly WinSWWrapper _winSW;
        private readonly string _testDirectory;

        public WinSWWrapperTests()
        {
            _winSW = new WinSWWrapper(NullLogger<WinSWWrapper>.Instance);
            _testDirectory = Path.Combine(Path.GetTempPath(), "WinServiceManagerTests");
            Directory.CreateDirectory(_testDirectory);
        }

        [Fact]
        public async Task InstallAsync_ShouldCreateServiceDirectory()
        {
            // Arrange
            var service = CreateTestService();

            // Act
            var result = await _winSW.InstallAsync(service);

            // Assert
            Directory.Exists(service.ServiceDirectory).Should().BeTrue();
            Directory.Exists(service.LogDirectory).Should().BeTrue();
        }

        [Fact]
        public async Task InstallAsync_ShouldCreateConfigFile()
        {
            // Arrange
            var service = CreateTestService();

            // Act
            var result = await _winSW.InstallAsync(service);

            // Assert
            File.Exists(service.WinSWConfigPath).Should().BeTrue();

            var configContent = await File.ReadAllTextAsync(service.WinSWConfigPath);
            configContent.Should().Contain($"<id>{service.Id}</id>");
            configContent.Should().Contain($"<name>{service.DisplayName}</name>");
        }

        [Fact]
        public void GetServiceStatus_WithNonExistentService_ShouldReturnNotInstalled()
        {
            // Arrange
            var serviceName = "NonExistentService";

            // Act
            var status = _winSW.GetServiceStatus(serviceName);

            // Assert
            status.Should().Be(ServiceStatus.NotInstalled);
        }

        private ServiceItem CreateTestService()
        {
            return new ServiceItem
            {
                Id = "test-service",
                DisplayName = "Test Service",
                ExecutablePath = Path.Combine(_testDirectory, "test.exe"),
                WorkingDirectory = _testDirectory
            };
        }

        public void Dispose()
        {
            if (Directory.Exists(_testDirectory))
            {
                try
                {
                    Directory.Delete(_testDirectory, true);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }
    }
}
```

### 3.4 ViewModel 测试
```csharp
// File: UnitTests/ViewModels/MainWindowViewModelTests.cs
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using WinServiceManager.Models;
using WinServiceManager.Services;
using WinServiceManager.ViewModels;
using Xunit;

namespace WinServiceManager.Tests.ViewModels
{
    public class MainWindowViewModelTests
    {
        private readonly Mock<ServiceManagerService> _mockServiceManager;
        private readonly Mock<LogReaderService> _mockLogReader;
        private readonly MainWindowViewModel _viewModel;

        public MainWindowViewModelTests()
        {
            _mockServiceManager = new Mock<ServiceManagerService>(
                Mock.Of<WinSWWrapper>(),
                Mock.Of<DataService>(),
                NullLogger<ServiceManagerService>.Instance);

            _mockLogReader = new Mock<LogReaderService>(NullLogger<LogReaderService>.Instance);

            _viewModel = new MainWindowViewModel(
                _mockServiceManager.Object,
                _mockLogReader.Object);
        }

        [Fact]
        public void Constructor_ShouldInitializeEmptyServicesList()
        {
            // Act & Assert
            _viewModel.Services.Should().NotBeNull();
            _viewModel.Services.Should().BeEmpty();
        }

        [Fact]
        public async Task CreateServiceCommand_ShouldOpenCreateDialog()
        {
            // Arrange
            _mockServiceManager.Setup(x => x.CreateServiceAsync(It.IsAny<ServiceCreateRequest>()))
                .ReturnsAsync(ServiceOperationResult.SuccessResult(ServiceOperationType.Install));

            // Act
            _viewModel.CreateServiceCommand.Execute(null);

            // Assert
            // 验证对话框打开（需要实际的 UI 测试框架）
        }

        [Fact]
        public void SearchText_WhenChanged_ShouldFilterServices()
        {
            // Arrange
            var services = new ObservableCollection<ServiceItemViewModel>
            {
                new(CreateService("Service A")),
                new(CreateService("Service B")),
                new(CreateService("Test Service"))
            };

            _viewModel.Services.Clear();
            foreach (var service in services)
            {
                _viewModel.Services.Add(service);
            }

            // Act
            _viewModel.SearchText = "Test";

            // Assert
            _viewModel.Services.Should().HaveCount(1);
            _viewModel.Services.First().DisplayName.Should().Be("Test Service");
        }

        private ServiceItem CreateService(string name)
        {
            return new ServiceItem
            {
                DisplayName = name,
                ExecutablePath = @"C:\test.exe",
                WorkingDirectory = @"C:\"
            };
        }
    }
}
```

## 4. 集成测试

### 4.1 数据持久化测试
```csharp
// File: IntegrationTests/DataPersistenceTests.cs
using WinServiceManager.Models;
using WinServiceManager.Services;
using Xunit;
using System.IO;

namespace WinServiceManager.Tests.IntegrationTests
{
    [Collection("Database Tests")]
    public class DataPersistenceTests : IDisposable
    {
        private readonly string _testDataFile;
        private readonly DataService _dataService;

        public DataPersistenceTests()
        {
            _testDataFile = Path.Combine(Path.GetTempPath(), $"test_data_{Guid.NewGuid()}.json");
            _dataService = new DataService(NullLogger<DataService>.Instance);

            // 设置测试数据文件路径
            typeof(DataService)
                .GetField("_dataFilePath", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(_dataService, _testDataFile);
        }

        [Fact]
        public async Task SaveAndLoadServices_ShouldPersistData()
        {
            // Arrange
            var services = new List<ServiceItem>
            {
                new ServiceItem
                {
                    Id = "test-1",
                    DisplayName = "Service 1",
                    ExecutablePath = @"C:\app1.exe",
                    WorkingDirectory = @"C:\app1"
                },
                new ServiceItem
                {
                    Id = "test-2",
                    DisplayName = "Service 2",
                    ExecutablePath = @"C:\app2.exe",
                    WorkingDirectory = @"C:\app2"
                }
            };

            // Act
            await _dataService.SaveServicesAsync(services);
            var loadedServices = await _dataService.LoadServicesAsync();

            // Assert
            loadedServices.Should().HaveCount(2);
            loadedServices.Should().BeEquivalentTo(services, options =>
                options.Excluding(s => s.CreatedAt)
                      .Excluding(s => s.UpdatedAt));
        }

        [Fact]
        public async Task LoadServices_WhenFileNotExists_ShouldReturnEmptyList()
        {
            // Act
            var services = await _dataService.LoadServicesAsync();

            // Assert
            services.Should().BeEmpty();
        }

        public void Dispose()
        {
            if (File.Exists(_testDataFile))
            {
                File.Delete(_testDataFile);
            }
        }
    }
}
```

## 5. 端到端测试

### 5.1 服务生命周期测试
```csharp
// File: E2ETests/ServiceLifecycleTests.cs
using System.Diagnostics;
using System.Threading;
using WinServiceManager.Models;
using WinServiceManager.Services;
using Xunit;
using Xunit.Abstractions;

namespace WinServiceManager.Tests.E2ETests
{
    [Collection("E2E Tests")]
    public class ServiceLifecycleTests : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly string _testDirectory;
        private readonly string _testScriptPath;

        public ServiceLifecycleTests(ITestOutputHelper output)
        {
            _output = output;
            _testDirectory = Path.Combine(Path.GetTempPath(), $"WinServiceE2E_{Guid.NewGuid()}");
            Directory.CreateDirectory(_testDirectory);

            // 创建测试脚本
            _testScriptPath = Path.Combine(_testDirectory, "test.py");
            File.WriteAllText(_testScriptPath, @"
import time
import sys

print('Service started')
sys.stdout.flush()

while True:
    print('Running...', time.time())
    sys.stdout.flush()
    time.sleep(1)
");
        }

        [Fact]
        public async Task CompleteServiceLifecycle_ShouldWorkCorrectly()
        {
            // Arrange
            var service = new ServiceItem
            {
                Id = $"test-service-{Guid.NewGuid()}",
                DisplayName = "E2E Test Service",
                ExecutablePath = FindPythonExecutable(),
                ScriptPath = _testScriptPath,
                WorkingDirectory = _testDirectory
            };

            var winSW = new WinSWWrapper(new ConsoleLogger<WinSWWrapper>(_output));
            var dataService = new DataService(new ConsoleLogger<DataService>(_output));
            var serviceManager = new ServiceManagerService(
                winSW,
                dataService,
                new ConsoleLogger<ServiceManagerService>(_output));

            await serviceManager.InitializeAsync();

            // Act & Assert - 安装
            _output.WriteLine("Installing service...");
            var installResult = await serviceManager.CreateServiceAsync(new ServiceCreateRequest
            {
                DisplayName = service.DisplayName,
                ExecutablePath = service.ExecutablePath,
                ScriptPath = service.ScriptPath,
                WorkingDirectory = service.WorkingDirectory,
                AutoStart = false
            });

            installResult.Success.Should().BeTrue();

            // 等待服务注册
            await Task.Delay(2000);

            // 验证服务存在
            var status = winSW.GetServiceStatus(service.Id);
            status.Should().Be(ServiceStatus.Stopped);

            // 启动服务
            _output.WriteLine("Starting service...");
            var startResult = await serviceManager.StartServiceAsync(service.Id);
            startResult.Success.Should().BeTrue();

            // 等待服务启动
            await Task.Delay(3000);

            // 验证服务正在运行
            status = winSW.GetServiceStatus(service.Id);
            status.Should().Be(ServiceStatus.Running);

            // 验证日志输出
            var logReader = new LogReaderService(new ConsoleLogger<LogReaderService>(_output));
            var logLines = await logReader.ReadLastLinesAsync(service.OutputLogPath, 10);
            logLines.Should().Contain(line => line.Contains("Service started"));

            // 停止服务
            _output.WriteLine("Stopping service...");
            var stopResult = await serviceManager.StopServiceAsync(service.Id);
            stopResult.Success.Should().BeTrue();

            // 等待服务停止
            await Task.Delay(2000);

            // 验证服务已停止
            status = winSW.GetServiceStatus(service.Id);
            status.Should().Be(ServiceStatus.Stopped);

            // 卸载服务
            _output.WriteLine("Uninstalling service...");
            var uninstallResult = await serviceManager.UninstallServiceAsync(service.Id);
            uninstallResult.Success.Should().BeTrue();

            // 验证服务已删除
            status = winSW.GetServiceStatus(service.Id);
            status.Should().Be(ServiceStatus.NotInstalled);
        }

        private string FindPythonExecutable()
        {
            var pythonPaths = new[]
            {
                @"C:\Python39\python.exe",
                @"C:\Python38\python.exe",
                @"C:\Python37\python.exe",
                @"C:\Program Files\Python39\python.exe",
                @"C:\Program Files\Python38\python.exe"
            };

            foreach (var path in pythonPaths)
            {
                if (File.Exists(path))
                {
                    return path;
                }
            }

            // 如果没找到，尝试在 PATH 中查找
            try
            {
                var process = Process.Start(new ProcessStartInfo
                {
                    FileName = "python.exe",
                    Arguments = "--version",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                });

                process.WaitForExit();
                if (process.ExitCode == 0)
                {
                    return "python.exe";
                }
            }
            catch
            {
                // Ignore
            }

            throw new InvalidOperationException("Python executable not found. Please install Python to run E2E tests.");
        }

        public void Dispose()
        {
            if (Directory.Exists(_testDirectory))
            {
                try
                {
                    Directory.Delete(_testDirectory, true);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }
    }

    // Helper console logger for tests
    public class ConsoleLogger<T> : ILogger<T>
    {
        private readonly ITestOutputHelper _output;

        public ConsoleLogger(ITestOutputHelper output)
        {
            _output = output;
        }

        public IDisposable BeginScope<TState>(TState state) => null!;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            _output.WriteLine($"[{logLevel}] {formatter(state, exception)}");
        }
    }
}
```

## 6. 性能测试

### 6.1 服务状态查询性能测试
```csharp
// File: PerformanceTests/ServiceStatusQueryTests.cs
using System.Diagnostics;
using System.Linq;
using WinServiceManager.Services;
using Xunit;
using Xunit.Abstractions;

namespace WinServiceManager.Tests.PerformanceTests
{
    public class ServiceStatusQueryTests
    {
        private readonly ITestOutputHelper _output;
        private readonly WinSWWrapper _winSW;

        public ServiceStatusQueryTests(ITestOutputHelper output)
        {
            _output = output;
            _winSW = new WinSWWrapper(NullLogger<WinSWWrapper>.Instance);
        }

        [Fact]
        public async Task QueryMultipleServicesStatus_ShouldCompleteWithinTimeLimit()
        {
            // Arrange
            var serviceNames = Enumerable.Range(1, 10)
                .Select(i => $"PerfTestService{i}")
                .ToList();

            var stopwatch = Stopwatch.StartNew();

            // Act
            foreach (var serviceName in serviceNames)
            {
                _winSW.GetServiceStatus(serviceName);
            }

            stopwatch.Stop();

            // Assert
            _output.WriteLine($"Queried {serviceNames.Count} services in {stopwatch.ElapsedMilliseconds}ms");
            stopwatch.ElapsedMilliseconds.Should().BeLessThan(1000); // Should complete within 1 second
        }
    }
}
```

## 7. 测试数据准备

### 测试脚本示例
```python
# TestData/SampleScripts/simple_service.py
import time
import sys
import logging

logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s [%(levelname)s] %(message)s',
    handlers=[
        logging.StreamHandler(sys.stdout),
        logging.FileHandler('app.log')
    ]
)

logger = logging.getLogger(__name__)

def main():
    logger.info("Service starting...")

    counter = 0
    while True:
        counter += 1
        logger.info(f"Service running... Count: {counter}")

        if counter % 10 == 0:
            logger.warning("This is a warning message")

        if counter % 50 == 0:
            logger.error("This is an error message (for testing)")

        time.sleep(1)

if __name__ == "__main__":
    try:
        main()
    except KeyboardInterrupt:
        logger.info("Service stopping...")
    except Exception as e:
        logger.error(f"Service error: {e}")
        sys.exit(1)
```

```batch
:: TestData/SampleScripts/simple_loop.bat
@echo off
:loop
echo [%date% %time%] Batch service is running...
timeout /t 2 /nobreak > nul
goto loop
```

## 8. 测试配置和运行

### GitHub Actions 工作流
```yaml
# .github/workflows/test.yml
name: Tests

on:
  push:
    branches: [ main, develop ]
  pull_request:
    branches: [ main ]

jobs:
  test:
    runs-on: windows-latest

    steps:
    - uses: actions/checkout@v3

    - name: Setup .NET
      uses: actions/setup-dotnet@v3
      with:
        dotnet-version: '8.0.x'

    - name: Restore dependencies
      run: dotnet restore

    - name: Build
      run: dotnet build --no-restore

    - name: Run unit tests
      run: dotnet test src/WinServiceManager.Tests --no-build --logger "trx;LogFileName=test_results.trx" --collect:"XPlat Code Coverage"

    - name: Run integration tests
      run: dotnet test src/WinServiceManager.Tests --no-build --logger "trx" --filter Category=Integration

    - name: Upload test results
      uses: actions/upload-artifact@v3
      if: always()
      with:
        name: test-results
        path: |
          **/*.trx
          **/coverage.cobertura.xml
```

### 运行测试命令
```powershell
# 运行所有测试
dotnet test

# 运行特定类别的测试
dotnet test --filter Category="Unit"
dotnet test --filter Category="Integration"
dotnet test --filter Category="E2E"

# 生成代码覆盖率报告
dotnet test --collect:"XPlat Code Coverage" --results-directory ./TestResults

# 运行特定测试
dotnet test --filter "FullyQualifiedName~ServiceItemTests"
```

## 9. 测试最佳实践

### 1. 测试命名约定
```csharp
[Fact]
public void MethodUnderTest_WhenCondition_ShouldExpectedBehavior()
{
    // Test implementation
}
```

### 2. AAA 模式
- **Arrange**: 设置测试数据和对象
- **Act**: 执行被测试的方法
- **Assert**: 验证结果

### 3. 测试隔离
- 每个测试应该独立运行
- 使用临时目录和文件
- 在测试后清理资源

### 4. Mock 使用
```csharp
// 使用 Moq 创建 mock
var mockService = new Mock<IService>();
mockService.Setup(x => x.DoSomething(It.IsAny<string>()))
    .Returns(true);

// Verify mock interactions
mockService.Verify(x => x.DoSomething("test"), Times.Once);
```

### 5. 异步测试
```csharp
[Fact]
public async Task AsyncMethod_ShouldCompleteSuccessfully()
{
    // Arrange
    var service = new Service();

    // Act
    var result = await service.DoSomethingAsync();

    // Assert
    result.Should().NotBeNull();
}
```

## 10. 持续集成测试策略

### 阶段性测试
1. **Pull Request**: 运行单元测试和快速集成测试
2. **Merge**: 运行完整测试套件
3. **Release**: 运行 E2E 测试和性能测试

### 测试报告
- 使用 `xunit.runner.visualstudio` 生成测试结果
- 使用 `coverlet` 生成代码覆盖率报告
- 集成到 Azure DevOps 或 GitHub Actions

### 质量门禁
- 代码覆盖率 > 80%
- 所有测试必须通过
- 性能测试不超过阈值