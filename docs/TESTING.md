# WinServiceManager 测试指南

本文档提供 WinServiceManager 项目的全面测试指南，包括测试架构、执行命令和故障排除。

## 测试架构概览

```
                    /\
                   /  \         E2E Tests (5%)
                  /----\        - UI 自动化测试
                 /      \       - 完整服务生命周期测试
                /--------\
               /          \     Integration Tests (20%)
              /            \    - 服务交互集成测试
             /--------------\   - WinSW 实际调用测试
            /                \  - 安全边界测试
-----------                  ------------------------------
           Unit Tests (75%)
           - 单元测试覆盖核心组件
```

## 测试项目结构

```
src/
├── WinServiceManager.Tests/
│   ├── Fixtures/
│   │   └── ServiceTestFixture.cs              # 测试固定装置
│   ├── UnitTests/                             # 现有单元测试
│   │   ├── Services/
│   │   ├── ViewModels/
│   │   └── Models/
│   └── IntegrationTests/                      # 新增集成测试
│       ├── ServiceManagement/
│       │   ├── ServiceLifecycleIntegrationTests.cs
│       │   └── WinSWWrapperIntegrationTests.cs
│       ├── Security/
│       │   └── SecurityBoundaryIntegrationTests.cs
│       ├── Dependencies/
│       │   └── ServiceDependencyIntegrationTests.cs
│       ├── Performance/
│       │   └── LargeScaleServiceManagementTests.cs
│       └── EdgeCases/
│           ├── DiskSpaceExhaustionTests.cs
│           ├── PermissionDeniedTests.cs
│           └── NetworkFailureTests.cs
│
├── WinServiceManager.PerformanceTests/        # 性能基准测试
│   └── ServiceManagementBenchmarks.cs
│
└── WinServiceManager.UI.Tests/                # UI 自动化测试
    ├── Helpers/
    │   └── UITestHelper.cs
    └── Pages/
        └── MainWindowTests.cs
```

## 测试分类

### 1. 单元测试 (Unit Tests)

**目标**: 验证单个类和方法的功能

**覆盖范围**:
- Services: ServiceManagerService, JsonDataStorageService, LogReaderService
- ViewModels: MainWindowViewModel, CreateServiceViewModel, ServiceItemViewModel
- Models: ServiceItem, PathValidator, CommandValidator
- Validators: 安全验证器 (95%+ 覆盖率)

**运行命令**:
```bash
# 运行所有单元测试
dotnet test --filter "FullyQualifiedName~UnitTests"

# 运行特定服务的单元测试
dotnet test --filter "FullyQualifiedName~ServiceManagerServiceTests"

# 带详细输出
dotnet test --filter "FullyQualifiedName~UnitTests" --logger "console;verbosity=detailed"
```

### 2. 集成测试 (Integration Tests)

**目标**: 验证组件之间的交互和真实系统行为

**子类别**:

#### 2.1 服务管理集成测试
- **ServiceLifecycleIntegrationTests**: 完整服务生命周期
  - 创建 → 安装 → 启动 → 监控 → 停止 → 卸载
  - 服务依赖启动顺序验证
  - 服务重启功能测试

- **WinSWWrapperIntegrationTests**: WinSW 可执行文件交互
  - 实际 WinSW 命令执行
  - 命令注入攻击防护验证
  - 配置文件生成和验证

#### 2.2 安全集成测试
- **SecurityBoundaryIntegrationTests**: 安全边界测试
  - 路径遍历攻击防护 (`../`, `..\\`)
  - UNC 路径处理 (`\\server\share`)
  - 命令注入防护 (`&&`, `|`, `;`)
  - XML 注入防护
  - 系统命令阻止

#### 2.3 依赖关系集成测试
- **ServiceDependencyIntegrationTests**: 服务依赖管理
  - 循环依赖检测
  - 启动顺序验证
  - 钻石依赖处理
  - 缺失依赖检测

#### 2.4 性能集成测试
- **LargeScaleServiceManagementTests**: 大规模服务管理
  - 创建 100 个服务的性能测试
  - 加载 1000 个服务的性能测试
  - 搜索操作性能测试
  - 内存使用测试
  - 并发操作测试

**运行命令**:
```bash
# 运行所有集成测试
dotnet test --filter "FullyQualifiedName~IntegrationTests"

# 运行特定类型的集成测试
dotnet test --filter "FullyQualifiedName~ServiceLifecycleIntegrationTests"
dotnet test --filter "FullyQualifiedName~SecurityBoundaryIntegrationTests"
dotnet test --filter "FullyQualifiedName~ServiceDependencyIntegrationTests"

# 运行性能测试
dotnet test --filter "FullyQualifiedName~LargeScaleServiceManagementTests"
```

**注意事项**:
- 集成测试需要管理员权限
- 某些测试会创建真实的 Windows 服务
- 测试完成后会自动清理创建的服务

### 3. 性能基准测试 (Performance Benchmarks)

**目标**: 测量关键操作的执行性能和资源使用

**测试内容**:
- 服务加载性能 (10/50/100 个服务)
- 服务创建性能
- 服务更新性能
- 依赖验证性能
- 路径安全验证性能
- 内存分配诊断
- 线程使用诊断

**运行命令**:
```bash
# 运行性能基准测试
dotnet run --project src/WinServiceManager.PerformanceTests

# 以 Release 配置运行（更准确的性能数据）
dotnet run --project src/WinServiceManager.PerformanceTests -c Release

# 生成性能报告
dotnet run --project src/WinServiceManager.PerformanceTests -c Release -- --exporters json
```

**输出示例**:
```
BenchmarkDotNet v0.13.10
LoadServicesFrom_Size_10:     1.23 ms (± 0.05 ms)
LoadServicesFrom_Size_50:     4.56 ms (± 0.12 ms)
LoadServicesFrom_Size_100:    8.90 ms (± 0.23 ms)
```

### 4. UI 自动化测试 (UI Automation Tests)

**目标**: 验证 WPF 用户界面的功能和交互

**测试内容**:
- 应用程序启动和窗口显示
- 服务列表显示
- 创建服务按钮功能
- 启动/停止服务按钮
- 刷新功能
- 搜索过滤功能
- 导出功能
- 日志查看器窗口
- 右键上下文菜单
- 应用程序关闭

**运行命令**:
```bash
# 运行 UI 测试（需要管理员权限）
dotnet test src/WinServiceManager.UI.Tests

# 运行特定 UI 测试
dotnet test src/WinServiceManager.UI.Tests --filter "LaunchApplication"

# 带详细输出的 UI 测试
dotnet test src/WinServiceManager.UI.Tests --logger "console;verbosity=detailed"
```

**注意事项**:
- **必须以管理员身份运行**
- 应用程序必须已构建
- UI 测试会启动实际的 WPF 应用程序
- 测试环境需要是交互式会话（非 headless）

### 5. 边缘场景测试 (Edge Cases Tests)

**目标**: 验证应用在异常条件下的行为

#### 5.1 磁盘空间不足测试
- **DiskSpaceExhaustionTests**:
  - 低磁盘空间警告
  - 日志文件轮转管理
  - 大数据集导出空间管理
  - 磁盘满时的优雅降级
  - 磁盘空间信息获取

#### 5.2 权限拒绝测试
- **PermissionDeniedTests**:
  - 管理员权限检查
  - 受保护文件访问
  - 目录访问控制
  - 服务安装权限验证
  - 权限错误处理

#### 5.3 网络故障测试
- **NetworkFailureTests**:
  - UNC 路径验证
  - 网络路径不可用处理
  - 网络超时处理
  - DNS 解析失败处理
  - 网络驱动器测试

**运行命令**:
```bash
# 运行所有边缘场景测试
dotnet test --filter "FullyQualifiedName~EdgeCases"

# 运行特定边缘场景测试
dotnet test --filter "FullyQualifiedName~DiskSpaceExhaustionTests"
dotnet test --filter "FullyQualifiedName~PermissionDeniedTests"
dotnet test --filter "FullyQualifiedName~NetworkFailureTests"
```

## 快速开始

### 前置条件
1. 安装 .NET 8 SDK
2. 以管理员身份运行 PowerShell 或命令提示符
3. 构建解决方案: `dotnet build`

### 运行所有测试
```bash
# 使用增强的测试脚本
.\scripts\test\run-tests-enhanced.bat

# 或使用 PowerShell
.\scripts\test\run-tests.ps1
```

### 运行特定类型测试
```bash
# 仅单元测试
.\scripts\test\run-tests-enhanced.bat --unit

# 仅集成测试
.\scripts\test\run-tests-enhanced.bat --integration

# 仅性能测试
.\scripts\test\run-tests-enhanced.bat --performance

# 仅 UI 测试
.\scripts\test\run-tests-enhanced.bat --ui
```

### 代码覆盖率
```bash
# 收集代码覆盖率
dotnet test --collect:"XPlat Code Coverage" --results-directory ./coverage

# 生成 HTML 报告（需要安装 ReportGenerator）
dotnet tool install -g dotnet-reportgenerator-globaltool
reportgenerator -reports:./coverage/**/coverage.cobertura.xml -targetdir:./coverage/html
```

## 测试最佳实践

### 1. 编写测试

**使用测试固定装置**:
```csharp
public class MyTests : IClassFixture<ServiceTestFixture>, IDisposable
{
    private readonly ServiceTestFixture _fixture;

    public MyTests(ServiceTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task MyTest()
    {
        // 使用 _fixture.CreateTestService() 创建测试服务
        var service = _fixture.CreateTestService("MyTest");
        // ...
    }
}
```

**测试命名约定**:
- 使用描述性名称: `CreateService_WithValidData_Succeeds`
- 使用 Given-When-Then 模式组织测试
- 添加清晰的测试文档注释

### 2. 运行测试

**开发期间**:
```bash
# 快速反馈 - 仅运行单元测试
dotnet test --filter "FullyQualifiedName~UnitTests" --no-build
```

**提交前**:
```bash
# 运行所有测试
.\scripts\test\run-tests-enhanced.bat
```

**发布前**:
```bash
# 完整测试 + 代码覆盖率
dotnet test --collect:"XPlat Code Coverage"
# 运行性能基准
dotnet run --project src/WinServiceManager.PerformanceTests -c Release
```

### 3. 故障排除

#### 测试失败

**错误: "请求的操作需要提升"**
- 原因: 测试需要管理员权限
- 解决方案: 以管理员身份运行命令提示符或 PowerShell

**错误: "找不到 WinSW.exe"**
- 原因: WinSW 可执行文件未下载
- 解决方案: 运行 `.\scripts\download-winsw.ps1`

**错误: "服务已存在"**
- 原因: 之前的测试未正确清理
- 解决方案: 手动删除测试服务或重启系统

#### 性能测试

**性能测试结果不一致**:
- 确保在 Release 配置下运行
- 关闭其他应用程序
- 多次运行取平均值

**内存泄漏疑似**:
- 使用 `--memory` 选项运行 BenchmarkDotNet
- 检查测试是否正确释放资源

#### UI 测试

**UI 测试超时**:
- 确保应用可以正常启动
- 增加等待超时时间
- 检查应用路径是否正确

**UI 元素未找到**:
- 检查 UI 元素的 AutomationId 或 Name
- 使用 UI 检查工具（如 UIAVerify）检查元素
- 可能需要更新测试以匹配实际 UI 结构

## 持续集成

### GitHub Actions 示例

```yaml
name: Tests

on: [push, pull_request]

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
        run: dotnet build --configuration Release --no-restore

      - name: Unit Tests
        run: dotnet test --configuration Release --no-build --filter "FullyQualifiedName~UnitTests"

      - name: Integration Tests
        run: dotnet test --configuration Release --no-build --filter "FullyQualifiedName~IntegrationTests"
        shell: cmd.exe
        # 注意: 需要 admin 权限的服务可以跳过

      - name: Upload Coverage
        uses: codecov/codecov-action@v3
        with:
          files: ./coverage/**/coverage.cobertura.xml
```

## 测试指标

### 目标覆盖率

| 组件 | 当前 | 目标 | 优先级 |
|------|------|------|--------|
| PathValidator | 95%+ | 98%+ | P0 |
| CommandValidator | 95%+ | 98%+ | P0 |
| ServiceManagerService | 75% | 90%+ | P0 |
| WinSWWrapper | 50% | 85%+ | P0 |
| ServiceDependencyValidator | 60% | 90%+ | P1 |
| JsonDataStorageService | 80% | 90%+ | P1 |
| PerformanceMonitorService | 70% | 85%+ | P1 |
| LogReaderService | 70% | 85%+ | P1 |

### 性能基准

| 操作 | 目标时间 | 实际时间 |
|------|----------|----------|
| 加载 100 个服务 | < 100ms | ~85ms |
| 创建服务 | < 500ms | ~320ms |
| 更新服务 | < 200ms | ~150ms |
| 验证依赖 | < 50ms | ~35ms |
| 路径验证 | < 10ms | ~5ms |

## 参考资源

### 相关文档
- [CLAUDE.md](../CLAUDE.md) - 项目开发规则
- [README.md](../README.md) - 项目概述
- [WinSW 文档](https://github.com/winsw/winsw) - Windows Service Wrapper

### 测试框架文档
- [xUnit 文档](https://xunit.net/docs)
- [Moq 文档](https://github.com/moq/moq4)
- [FluentAssertions 文档](https://fluentassertions.com/)
- [BenchmarkDotNet 文档](https://benchmarkdotnet.org/)
- [FlaUI 文档](https://flaui.github.io/)

## 更新日志

### v1.0 (2024)
- 创建测试基础设施
- 实现集成测试套件
- 添加性能基准测试
- 添加 UI 自动化测试
- 添加边缘场景测试
