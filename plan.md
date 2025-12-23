# 服务状态自动更新改进实施计划

## 问题描述

用户在服务界面启动、停止、重启一个服务后，虽然执行成功，但需要手动刷新才能看到最终状态。当快速连续操作多个服务时（如启动服务A后立即停止服务B），独立的状态轮询可能导致资源消耗累积。

## 问题分析

### 当前实现机制

1. **ServiceStatusMonitor**：使用 `System.Threading.Timer` 每 30 秒轮询一次所有服务状态
2. **ServiceItemViewModel.StartStatusPolling()**：操作成功后启动独立轮询，每 500ms 检查一次，最多 30 次（15 秒）
3. **状态更新流程**：
   - 用户点击操作 → 设置过渡状态（如 Starting） → 执行操作 → 启动独立轮询
   - 独立轮询通过 `StatusRefreshRequested` 事件通知主窗口
   - 主窗口调用 `GetActualServiceStatus()` 获取真实状态并更新 UI

### 问题根源

| 问题 | 影响 |
|------|------|
| 监控间隔过长（30秒） | 操作后需等待最多 30 秒才能看到状态变化 |
| 独立轮询无协调 | 多服务操作时每个服务独立轮询，资源消耗线性增长 |
| 轮询超时过短（15秒） | 某些服务启动/停止时间较长，可能未完成就停止轮询 |
| 依赖事件逐个通知 | `StatusRefreshRequested` 每次只通知一个服务 |

### 多服务并发场景分析

**用户操作**：启动服务A → 立即停止服务B

**当前行为**：
```
启动服务A → StartStatusPolling() → 每 500ms 检查一次 A（最多 30 次）
停止服务B → StartStatusPolling() → 每 500ms 检查一次 B（最多 30 次）

总计：60 次 ServiceController 调用（分散在 15 秒内）
```

**潜在问题**：
1. Windows SCM（Service Control Manager）压力增加
2. UI 线程可能因多个状态更新请求而繁忙
3. 每个服务独立管理轮询，无法共享状态检查

## 解决方案设计

### 方案选择：全局轮询协调器

采用**全局集中式轮询协调器**替代各服务的独立轮询，实现：
- 统一管理所有待监控服务
- 动态调整轮询间隔
- 批量获取状态以减少 SCM 调用
- 自动清理已稳定的服务

### 架构设计

```
┌─────────────────────────────────────────────────────────────┐
│                    ServiceStatusMonitor                      │
│  （现有：30秒全局轮询，保持不变）                              │
└─────────────────────────────────────────────────────────────┘
                              │
                              │ 订阅全局状态更新
                              ▼
┌─────────────────────────────────────────────────────────────┐
│              ServicePollingCoordinator (新增)                 │
│  ┌─────────────────────────────────────────────────────┐    │
│  │ PendingServices: HashSet<string>                    │    │
│  │ - 记录需要高频轮询的服务ID                            │    │
│  │ - 操作后自动添加，状态稳定后自动移除                  │    │
│  └─────────────────────────────────────────────────────┘    │
│  ┌─────────────────────────────────────────────────────┐    │
│  │ Timer: 高频轮询定时器                                 │    │
│  │ - 默认 1 秒间隔                                       │    │
│  │ - 根据待监控服务数量动态调整                          │    │
│  │ - 无待监控服务时自动停止                              │    │
│  └─────────────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────────────┘
         ▲                                    │
         │ 注册协调器                          │ 批量状态更新
         │                                    ▼
┌─────────────────────────┐         ┌──────────────────────────┐
│  ServiceItemViewModel   │         │  MainWindowViewModel      │
│  - 操作后调用           │         │  - 接收批量状态更新        │
│    AddPendingService()  │         │  - 统一更新 UI            │
└─────────────────────────┘         └──────────────────────────┘
```

### 核心类设计

#### 1. ServicePollingCoordinator（新增）

```csharp
public class ServicePollingCoordinator : IDisposable
{
    // 待监控的服务ID集合
    private readonly HashSet<string> _pendingServices = new();

    // 服务加入时间记录（用于超时清理）
    private readonly Dictionary<string, DateTime> _serviceAddedTime = new();

    // 高频轮询定时器
    private Timer? _pollingTimer;

    // 轮询间隔（毫秒）- 根据待监控服务数量动态调整
    private int _currentInterval = 1000;

    // 最大监控时长（秒）- 防止服务卡在过渡状态
    private const int MAX_MONITORING_DURATION = 30;

    // 依赖服务
    private readonly ServiceManagerService _serviceManager;
    private readonly ILogger<ServicePollingCoordinator> _logger;

    // 状态更新事件
    public event EventHandler<ServicesUpdatedEventArgs>? ServicesUpdated;
}

public class ServicesUpdatedEventArgs : EventArgs
{
    public Dictionary<string, ServiceStatus> StatusUpdates { get; set; } = new();
}
```

#### 2. 动态轮询间隔策略

```csharp
private int GetPollingInterval(int pendingCount)
{
    return pendingCount switch
    {
        0 => 0,              // 无待监控服务：停止轮询
        1 => 500,            // 1个服务：500ms 快速响应
        <= 3 => 1000,        // 2-3个服务：1秒
        <= 5 => 1500,        // 4-5个服务：1.5秒
        _ => 2000            // 6+个服务：2秒，避免过载
    };
}
```

#### 3. 批量状态获取

```csharp
private async Task<Dictionary<string, ServiceStatus>> GetPendingServicesStatusAsync()
{
    var statusMap = new Dictionary<string, ServiceStatus>();

    // 并发获取所有待监控服务的状态（限制并发数）
    var semaphore = new SemaphoreSlim(5); // 最多同时5个请求
    var tasks = _pendingServices.Select(async serviceId =>
    {
        await semaphore.WaitAsync();
        try
        {
            // 需要从 ServiceManagerService 支持按ID获取服务
            var status = await _serviceManager.GetServiceStatusByIdAsync(serviceId);
            return (serviceId, status);
        }
        finally
        {
            semaphore.Release();
        }
    });

    var results = await Task.WhenAll(tasks);
    foreach (var (serviceId, status) in results)
    {
        statusMap[serviceId] = status;
    }

    return statusMap;
}
```

### 状态更新流程

```
用户操作（启动服务A）
    │
    ▼
ServiceItemViewModel.StartAsync()
    │
    ▼
设置 Status = Starting
    │
    ▼
执行启动操作
    │
    ├── 成功 → _pollingCoordinator.AddPendingService(serviceId)
    │          │
    │          ▼
    │     协调器添加服务A到待监控集合
    │          │
    │          ▼
    │     启动/调整定时器（1个服务 → 500ms间隔）
    │          │
    │          ▼
    │     定时器触发 → 批量获取状态
    │          │
    │          ├── 服务A状态稳定 → 从集合移除
    │          │
    │          └── 触发 ServicesUpdated 事件
    │                 │
    │                 ▼
    │            MainWindowViewModel 批量更新 UI
    │
    └── 失败 → 显示错误，状态回退
```

### 多服务并发场景优化

**场景**：启动服务A → 立即停止服务B

```
时间轴：
0.0s  启动A → AddPendingService("A") → 启动定时器（500ms）
0.1s  停止B → AddPendingService("B") → 调整定时器（1s，2个服务）
1.0s  定时器触发 → 批量检查 {A, B}
      - A 状态：Starting → 保留
      - B 状态：Stopping → 保留
2.0s  定时器触发 → 批量检查 {A, B}
      - A 状态：Running → 移除A
      - B 状态：Stopped → 移除B
      集合为空 → 停止定时器

总计：2次批量检查 = 4次 ServiceController 调用
对比原方案：60次独立检查
优化：减少 93% 的调用次数
```

## 实施步骤

### 第一步：创建 ServicePollingCoordinator 类

**文件路径**：`src/WinServiceManager/Services/ServicePollingCoordinator.cs`

**核心方法**：
1. `AddPendingService(string serviceId)` - 添加待监控服务
2. `RemovePendingService(string serviceId)` - 移除服务
3. `StartPolling()` - 启动轮询
4. `StopPolling()` - 停止轮询
5. `PollPendingServices()` - 轮询核心逻辑
6. `GetPollingInterval(int count)` - 动态间隔计算

### 第二步：扩展 ServiceManagerService

**新增方法**：
```csharp
// 根据服务ID获取服务状态（避免每次都加载所有服务）
public async Task<ServiceStatus> GetServiceStatusByIdAsync(string serviceId)
{
    // 从数据存储加载指定服务
    var service = await _dataStorage.LoadServiceAsync(serviceId);
    if (service == null) return ServiceStatus.NotInstalled;

    return GetActualServiceStatus(service);
}
```

### 第三步：修改 ServiceItemViewModel

**变更内容**：
1. 移除 `StartStatusPolling()` 方法
2. 移除 `_statusPollingCts` 字段
3. 操作成功后调用 `_pollingCoordinator.AddPendingService(Service.Id)`

**修改前**：
```csharp
private void StartStatusPolling()
{
    _statusPollingCts?.Cancel();
    _statusPollingCts = new CancellationTokenSource();
    _ = Task.Run(async () => { /* 轮询逻辑 */ });
}
```

**修改后**：
```csharp
// 操作成功后
if (startResult.Success)
{
    _pollingCoordinator.AddPendingService(Service.Id);
}
```

### 第四步：修改 MainWindowViewModel

**变更内容**：
1. 注入 `ServicePollingCoordinator`
2. 订阅 `ServicesUpdated` 事件
3. 实现批量状态更新逻辑

**新增方法**：
```csharp
private void OnServicesUpdated(object? sender, ServicesUpdatedEventArgs e)
{
    Application.Current.Dispatcher.Invoke(() =>
    {
        foreach (var (serviceId, status) in e.StatusUpdates)
        {
            var viewModel = _allServices.FirstOrDefault(vm => vm.Service.Id == serviceId);
            if (viewModel != null)
            {
                viewModel.UpdateStatus(status);
                viewModel.RefreshCommands();
            }
        }
    });
}
```

### 第五步：注册依赖注入

**文件**：`src/WinServiceManager/App.xaml.cs`

**修改内容**：
```csharp
services.AddSingleton<ServicePollingCoordinator>();
```

### 第六步：调整 ServiceStatusMonitor 默认间隔

**文件**：`src/WinServiceManager/Services/ServiceStatusMonitor.cs`

**修改内容**：
```csharp
// 将默认间隔从 30 秒改为 10 秒
public void StartMonitoring(int intervalSeconds = 10)
```

### 第七步：扩展 JsonDataStorageService

**新增方法**（如果不存在）：
```csharp
public async Task<ServiceItem?> LoadServiceAsync(string serviceId)
{
    var services = await LoadServicesAsync();
    return services.FirstOrDefault(s => s.Id == serviceId);
}
```

## 文件清单

### 新增文件
- `src/WinServiceManager/Services/ServicePollingCoordinator.cs`
- `src/WinServiceManager/Models/ServicesUpdatedEventArgs.cs`

### 修改文件
- `src/WinServiceManager/Services/ServiceManagerService.cs` - 新增 GetServiceStatusByIdAsync
- `src/WinServiceManager/Services/ServiceStatusMonitor.cs` - 调整默认间隔
- `src/WinServiceManager/ViewModels/ServiceItemViewModel.cs` - 移除独立轮询，使用协调器
- `src/WinServiceManager/ViewModels/MainWindowViewModel.cs` - 订阅协调器事件
- `src/WinServiceManager/Services/JsonDataStorageService.cs` - 新增 LoadServiceAsync
- `src/WinServiceManager/App.xaml.cs` - 注册协调器

## 测试验证

### 测试场景

#### 场景 1：单服务操作
1. 启动一个已停止的服务
2. 观察 UI 状态变化是否流畅（无延迟）
3. 验证状态最终正确显示为"运行中"

#### 场景 2：多服务快速操作
1. 启动服务A
2. 立即停止服务B
3. 观察两个服务的状态是否都能正确更新
4. 检查日志确认轮询调用次数合理

#### 场景 3：长时间启动的服务
1. 启动一个需要较长时间启动的服务（如 10 秒）
2. 验证状态能在 30 秒内正确更新
3. 确认超时机制正常工作

#### 场景 4：状态异常
1. 启动一个会失败的服务
2. 验证错误处理正确
3. 确认服务从待监控集合中移除

### 性能指标

| 指标 | 目标值 |
|------|--------|
| 单服务状态更新延迟 | < 2 秒 |
| 多服务（5个）并发操作 | 无 UI 卡顿 |
| SCM 调用次数（5服务操作） | < 20 次 |
| 内存增加 | < 5 MB |

## 风险与缓解

| 风险 | 影响 | 缓解措施 |
|------|------|----------|
| 协调器单点故障 | 所有状态更新失效 | 添加异常处理，失败后回退到独立轮询 |
| 批量查询性能问题 | UI 响应变慢 | 限制并发数量，使用信号量 |
| 服务状态不同步 | UI 显示错误状态 | 保留 ServiceStatusMonitor 作为兜底 |
| 定时器未正确停止 | 资源泄漏 | 实现 IDisposable，添加超时清理 |

## 实施时间表

| 阶段 | 任务 | 预计工作量 |
|------|------|-----------|
| 1 | 创建 ServicePollingCoordinator 类 | 2 小时 |
| 2 | 扩展 ServiceManagerService 和 JsonDataStorageService | 1 小时 |
| 3 | 修改 ServiceItemViewModel | 1 小时 |
| 4 | 修改 MainWindowViewModel | 1 小时 |
| 5 | 更新依赖注入配置 | 0.5 小时 |
| 6 | 调整 ServiceStatusMonitor | 0.5 小时 |
| 7 | 单元测试 | 2 小时 |
| 8 | 集成测试与调试 | 2 小时 |
| **总计** | | **10 小时** |

## 回退计划

如果新方案出现问题，可快速回退：
1. 恢复 `ServiceItemViewModel.StartStatusPolling()` 方法
2. 移除协调器的依赖注入
3. 注释掉协调器相关调用

回退时间：< 30 分钟

## 参考资料

- Microsoft.Extensions.DependencyInjection 文档
- System.Threading.Timer 最佳实践
- WPF MVVM 模式指南
- Windows Service Control Manager API
