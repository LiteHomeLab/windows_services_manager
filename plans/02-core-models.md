# 核心模型设计

## 数据模型概览

本文档定义了 WinServiceManager 项目的核心数据模型，包括服务实体、状态枚举和相关的数据传输对象。

## 1. ServiceItem - 服务实体模型

### 类定义
```csharp
// File: Models/ServiceItem.cs
using System;

namespace WinServiceManager.Models
{
    /// <summary>
    /// 表示一个通过 WinServiceManager 创建的服务
    /// </summary>
    public class ServiceItem
    {
        /// <summary>
        /// 服务的唯一标识符 (GUID)
        /// 用作：目录名、WinSW 可执行文件名、服务 ID
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        /// <summary>
        /// 服务在 UI 中显示的名称
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// 服务描述信息
        /// </summary>
        public string Description { get; set; } = "Managed by WinServiceManager";

        /// <summary>
        /// 可执行文件路径 (如 python.exe 或 app.exe)
        /// </summary>
        public string ExecutablePath { get; set; } = string.Empty;

        /// <summary>
        /// 脚本文件路径 (如果可执行文件是解释器)
        /// </summary>
        public string? ScriptPath { get; set; }

        /// <summary>
        /// 启动参数
        /// </summary>
        public string Arguments { get; set; } = string.Empty;

        /// <summary>
        /// 工作目录
        /// </summary>
        public string WorkingDirectory { get; set; } = string.Empty;

        /// <summary>
        /// 服务当前状态
        /// </summary>
        public ServiceStatus Status { get; set; } = ServiceStatus.NotInstalled;

        /// <summary>
        /// 服务创建时间
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        /// <summary>
        /// 服务最后更新时间
        /// </summary>
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        /// <summary>
        /// 服务目录路径
        /// </summary>
        public string ServiceDirectory => Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "services",
            Id
        );

        /// <summary>
        /// WinSW 可执行文件路径
        /// </summary>
        public string WinSWExecutablePath => Path.Combine(
            ServiceDirectory,
            $"{Id}.exe"
        );

        /// <summary>
        /// WinSW 配置文件路径
        /// </summary>
        public string WinSWConfigPath => Path.Combine(
            ServiceDirectory,
            $"{Id}.xml"
        );

        /// <summary>
        /// 日志目录路径
        /// </summary>
        public string LogDirectory => Path.Combine(
            ServiceDirectory,
            "logs"
        );

        /// <summary>
        /// 标准输出日志文件路径
        /// </summary>
        public string OutputLogPath => Path.Combine(
            LogDirectory,
            $"{Id}.out.log"
        );

        /// <summary>
        /// 错误输出日志文件路径
        /// </summary>
        public string ErrorLogPath => Path.Combine(
            LogDirectory,
            $"{Id}.err.log"
        );

        /// <summary>
        /// 生成完整的启动参数（包含脚本路径）
        /// </summary>
        public string GetFullArguments()
        {
            if (!string.IsNullOrEmpty(ScriptPath))
            {
                // 如果有脚本路径，将脚本路径添加到参数前
                return $"\"{ScriptPath}\" {Arguments}";
            }
            return Arguments;
        }

        /// <summary>
        /// 生成 WinSW 配置文件内容
        /// </summary>
        public string GenerateWinSWConfig()
        {
            var xml = $@"<service>
  <id>{Id}</id>
  <name>{DisplayName}</name>
  <description>{Description}</description>
  <executable>{ExecutablePath}</executable>
  <arguments>{GetFullArguments()}</arguments>
  <workingdirectory>{WorkingDirectory}</workingdirectory>
  <log mode=""roll-by-size"">
    <sizeThreshold>10240</sizeThreshold>
    <keepFiles>8</keepFiles>
  </log>
  <stopparentprocessfirst>true</stopparentprocessfirst>
</service>";
            return xml;
        }
    }
}
```

### 属性说明

| 属性 | 类型 | 说明 | 必需 |
|-----|------|------|------|
| Id | string | 服务唯一标识符 | 是 |
| DisplayName | string | 显示名称 | 是 |
| Description | string | 服务描述 | 否 |
| ExecutablePath | string | 可执行文件路径 | 是 |
| ScriptPath | string? | 脚本文件路径 | 否 |
| Arguments | string | 启动参数 | 否 |
| WorkingDirectory | string | 工作目录 | 是 |
| Status | ServiceStatus | 服务状态 | 是 |
| CreatedAt | DateTime | 创建时间 | 是 |
| UpdatedAt | DateTime | 更新时间 | 是 |

## 2. ServiceStatus - 服务状态枚举

### 枚举定义
```csharp
// File: Models/ServiceStatus.cs
namespace WinServiceManager.Models
{
    /// <summary>
    /// 服务状态枚举
    /// </summary>
    public enum ServiceStatus
    {
        /// <summary>
        /// 未安装
        /// </summary>
        NotInstalled = 0,

        /// <summary>
        /// 已停止
        /// </summary>
        Stopped = 1,

        /// <summary>
        /// 正在运行
        /// </summary>
        Running = 2,

        /// <summary>
        /// 正在启动
        /// </summary>
        Starting = 3,

        /// <summary>
        /// 正在停止
        /// </summary>
        Stopping = 4,

        /// <summary>
        /// 已暂停
        /// </summary>
        Paused = 5,

        /// <summary>
        /// 错误状态
        /// </summary>
        Error = 6,

        /// <summary>
        /// 正在安装
        /// </summary>
        Installing = 7,

        /// <summary>
        /// 正在卸载
        /// </summary>
        Uninstalling = 8
    }

    /// <summary>
    /// ServiceStatus 扩展方法
    /// </summary>
    public static class ServiceStatusExtensions
    {
        /// <summary>
        /// 获取状态的显示文本
        /// </summary>
        public static string GetDisplayText(this ServiceStatus status)
        {
            return status switch
            {
                ServiceStatus.NotInstalled => "未安装",
                ServiceStatus.Stopped => "已停止",
                ServiceStatus.Running => "运行中",
                ServiceStatus.Starting => "启动中",
                ServiceStatus.Stopping => "停止中",
                ServiceStatus.Paused => "已暂停",
                ServiceStatus.Error => "错误",
                ServiceStatus.Installing => "安装中",
                ServiceStatus.Uninstalling => "卸载中",
                _ => "未知"
            };
        }

        /// <summary>
        /// 判断是否为中间状态
        /// </summary>
        public static bool IsTransitioning(this ServiceStatus status)
        {
            return status switch
            {
                ServiceStatus.Starting or
                ServiceStatus.Stopping or
                ServiceStatus.Installing or
                ServiceStatus.Uninstalling => true,
                _ => false
            };
        }

        /// <summary>
        /// 判断是否可以进行启动操作
        /// </summary>
        public static bool CanStart(this ServiceStatus status)
        {
            return status == ServiceStatus.Stopped || status == ServiceStatus.NotInstalled;
        }

        /// <summary>
        /// 判断是否可以进行停止操作
        /// </summary>
        public static bool CanStop(this ServiceStatus status)
        {
            return status == ServiceStatus.Running || status == ServiceStatus.Starting;
        }

        /// <summary>
        /// 判断是否可以进行卸载操作
        /// </summary>
        public static bool CanUninstall(this ServiceStatus status)
        {
            return status == ServiceStatus.Stopped || status == ServiceStatus.NotInstalled;
        }
    }
}
```

## 3. ServiceCreateRequest - 创建服务请求模型

### 类定义
```csharp
// File: Models/ServiceCreateRequest.cs
using System.ComponentModel.DataAnnotations;

namespace WinServiceManager.Models
{
    /// <summary>
    /// 创建服务的请求模型
    /// </summary>
    public class ServiceCreateRequest
    {
        /// <summary>
        /// 服务显示名称
        /// </summary>
        [Required(ErrorMessage = "服务名称不能为空")]
        [StringLength(100, MinimumLength = 3, ErrorMessage = "服务名称长度必须在3-100个字符之间")]
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// 服务描述
        /// </summary>
        [StringLength(500, ErrorMessage = "描述长度不能超过500个字符")]
        public string? Description { get; set; }

        /// <summary>
        /// 可执行文件路径
        /// </summary>
        [Required(ErrorMessage = "请选择可执行文件")]
        [FileExists(ErrorMessage = "指定的可执行文件不存在")]
        public string ExecutablePath { get; set; } = string.Empty;

        /// <summary>
        /// 脚本文件路径（可选）
        /// </summary>
        [FileExists(ErrorMessage = "指定的脚本文件不存在")]
        public string? ScriptPath { get; set; }

        /// <summary>
        /// 启动参数
        /// </summary>
        public string Arguments { get; set; } = string.Empty;

        /// <summary>
        /// 工作目录
        /// </summary>
        [Required(ErrorMessage = "请选择工作目录")]
        [DirectoryExists(ErrorMessage = "指定的工作目录不存在")]
        public string WorkingDirectory { get; set; } = string.Empty;

        /// <summary>
        /// 创建后是否自动启动服务
        /// </summary>
        public bool AutoStart { get; set; } = true;
    }

    /// <summary>
    /// 验证文件是否存在
    /// </summary>
    public class FileExistsAttribute : ValidationAttribute
    {
        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            if (value is string path && !string.IsNullOrEmpty(path))
            {
                if (!File.Exists(path))
                {
                    return new ValidationResult(ErrorMessage);
                }
            }
            return ValidationResult.Success;
        }
    }

    /// <summary>
    /// 验证目录是否存在
    /// </summary>
    public class DirectoryExistsAttribute : ValidationAttribute
    {
        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            if (value is string path && !string.IsNullOrEmpty(path))
            {
                if (!Directory.Exists(path))
                {
                    return new ValidationResult(ErrorMessage);
                }
            }
            return ValidationResult.Success;
        }
    }
}
```

## 4. ServiceOperationResult - 操作结果模型

### 类定义
```csharp
// File: Models/ServiceOperationResult.cs
namespace WinServiceManager.Models
{
    /// <summary>
    /// 服务操作结果
    /// </summary>
    public class ServiceOperationResult
    {
        /// <summary>
        /// 操作是否成功
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// 错误信息（如果操作失败）
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// 详细的错误信息
        /// </summary>
        public string? Details { get; set; }

        /// <summary>
        /// 操作类型
        /// </summary>
        public ServiceOperationType Operation { get; set; }

        /// <summary>
        /// 操作耗时（毫秒）
        /// </summary>
        public long ElapsedMilliseconds { get; set; }

        /// <summary>
        /// 创建成功结果
        /// </summary>
        public static ServiceOperationResult SuccessResult(ServiceOperationType operation, long elapsedMs = 0)
        {
            return new ServiceOperationResult
            {
                Success = true,
                Operation = operation,
                ElapsedMilliseconds = elapsedMs
            };
        }

        /// <summary>
        /// 创建失败结果
        /// </summary>
        public static ServiceOperationResult FailureResult(
            ServiceOperationType operation,
            string errorMessage,
            string? details = null,
            long elapsedMs = 0)
        {
            return new ServiceOperationResult
            {
                Success = false,
                Operation = operation,
                ErrorMessage = errorMessage,
                Details = details,
                ElapsedMilliseconds = elapsedMs
            };
        }
    }

    /// <summary>
    /// 服务操作类型
    /// </summary>
    public enum ServiceOperationType
    {
        Install,
        Uninstall,
        Start,
        Stop,
        Restart,
        Update,
        QueryStatus
    }
}
```

## 5. LogEntry - 日志条目模型

### 类定义
```csharp
// File: Models/LogEntry.cs
namespace WinServiceManager.Models
{
    /// <summary>
    /// 日志条目
    /// </summary>
    public class LogEntry
    {
        /// <summary>
        /// 日志时间戳
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// 日志级别
        /// </summary>
        public LogLevel Level { get; set; }

        /// <summary>
        /// 日志内容
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// 日志来源（进程ID）
        /// </summary>
        public int ProcessId { get; set; }

        /// <summary>
        /// 原始日志行
        /// </summary>
        public string RawLine { get; set; } = string.Empty;
    }

    /// <summary>
    /// 日志级别
    /// </summary>
    public enum LogLevel
    {
        Unknown,
        Info,
        Warning,
        Error,
        Debug
    }
}
```

## 6. 数据验证和转换

### ServiceItem 验证规则
```csharp
public class ServiceItemValidator
{
    /// <summary>
    /// 验证 ServiceItem 是否有效
    /// </summary>
    public static (bool IsValid, List<string> Errors) Validate(ServiceItem service)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(service.DisplayName))
        {
            errors.Add("服务名称不能为空");
        }

        if (string.IsNullOrWhiteSpace(service.ExecutablePath))
        {
            errors.Add("可执行文件路径不能为空");
        }
        else if (!File.Exists(service.ExecutablePath))
        {
            errors.Add("可执行文件不存在");
        }

        if (string.IsNullOrWhiteSpace(service.WorkingDirectory))
        {
            errors.Add("工作目录不能为空");
        }
        else if (!Directory.Exists(service.WorkingDirectory))
        {
            errors.Add("工作目录不存在");
        }

        // 验证脚本路径（如果指定）
        if (!string.IsNullOrEmpty(service.ScriptPath))
        {
            if (!File.Exists(service.ScriptPath))
            {
                errors.Add("脚本文件不存在");
            }
        }

        return (errors.Count == 0, errors);
    }
}
```

## 7. 数据持久化格式

### JSON 存储格式
```json
{
  "version": "1.0",
  "services": [
    {
      "id": "a1b2c3d4e5f6",
      "displayName": "My Python Service",
      "description": "A Python script service",
      "executablePath": "C:\\Python39\\python.exe",
      "scriptPath": "D:\\Scripts\\main.py",
      "arguments": "--prod --verbose",
      "workingDirectory": "D:\\Scripts",
      "status": 2,
      "createdAt": "2024-01-15T10:30:00Z",
      "updatedAt": "2024-01-15T10:35:00Z"
    }
  ],
  "settings": {
    "lastRefreshTime": "2024-01-15T11:00:00Z",
    "autoRefreshInterval": 30
  }
}
```

## 8. 使用示例

### 创建新服务
```csharp
var request = new ServiceCreateRequest
{
    DisplayName = "My Python Service",
    Description = "A Python script that runs continuously",
    ExecutablePath = @"C:\Python39\python.exe",
    ScriptPath = @"D:\Scripts\main.py",
    Arguments = "--prod",
    WorkingDirectory = @"D:\Scripts"
};

var service = new ServiceItem
{
    DisplayName = request.DisplayName,
    Description = request.Description,
    ExecutablePath = request.ExecutablePath,
    ScriptPath = request.ScriptPath,
    Arguments = request.Arguments,
    WorkingDirectory = request.WorkingDirectory
};

// 生成 WinSW 配置
var config = service.GenerateWinSWConfig();
```

### 检查服务状态
```csharp
if (service.Status.CanStart())
{
    // 可以启动服务
}

if (service.Status.IsTransitioning())
{
    // 服务状态正在转换中
    // 禁用操作按钮或显示加载动画
}
```

## 9. 注意事项

1. **路径处理**: 所有路径都应使用 `Path.Combine()` 来确保跨平台兼容性
2. **GUID 生成**: 使用 `Guid.NewGuid().ToString("N")` 生成无分隔符的 GUID
3. **时间格式**: 使用 ISO 8601 格式存储时间
4. **序列化**: 使用 Newtonsoft.Json 进行 JSON 序列化，配置忽略 null 值
5. **验证**: 所有用户输入都应进行验证，特别是文件路径
6. **安全性**: 避免路径遍历攻击，验证路径是否在允许的范围内