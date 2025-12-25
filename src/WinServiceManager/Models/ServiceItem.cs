using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Xml.Linq;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using WinServiceManager.ViewModels;

namespace WinServiceManager.Models
{
    /// <summary>
    /// 表示一个通过 WinServiceManager 创建的服务
    /// </summary>
    public class ServiceItem : BaseViewModel
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

        private string _executablePath = string.Empty;

        /// <summary>
        /// 可执行文件路径 (如 python.exe 或 app.exe)
        /// </summary>
        public string ExecutablePath
        {
            get => _executablePath;
            set
            {
                if (!string.IsNullOrEmpty(value) && !PathValidator.IsValidPath(value))
                    throw new ArgumentException($"Invalid executable path: {value}");
                _executablePath = value;
            }
        }

        private string? _scriptPath;

        /// <summary>
        /// 脚本文件路径 (如果可执行文件是解释器)
        /// </summary>
        public string? ScriptPath
        {
            get => _scriptPath;
            set
            {
                if (!string.IsNullOrEmpty(value) && !PathValidator.IsValidPath(value))
                    throw new ArgumentException($"Invalid script path: {value}");
                _scriptPath = value;
            }
        }

        /// <summary>
        /// 启动参数
        /// </summary>
        public string Arguments { get; set; } = string.Empty;

        private string _workingDirectory = string.Empty;

        /// <summary>
        /// 工作目录
        /// </summary>
        public string WorkingDirectory
        {
            get => _workingDirectory;
            set
            {
                if (!string.IsNullOrEmpty(value) && !PathValidator.IsValidPath(value))
                    throw new ArgumentException($"Invalid working directory: {value}");
                _workingDirectory = value;
            }
        }

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
        /// 依赖的服务列表（服务ID）
        /// </summary>
        public List<string> Dependencies { get; set; } = new List<string>();

        /// <summary>
        /// 环境变量键值对
        /// </summary>
        public Dictionary<string, string> EnvironmentVariables { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// 是否被选中为依赖服务（用于UI选择）
        /// </summary>
        private bool _isSelected = false;

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        /// <summary>
        /// 启动参数（别名为Arguments，用于导出兼容性）
        /// </summary>
        public string StartupArguments
        {
            get => Arguments;
            set => Arguments = value;
        }

        /// <summary>
        /// 服务账户
        /// </summary>
        public string? ServiceAccount { get; set; }

        /// <summary>
        /// 环境变量（别名为EnvironmentVariables，用于导出兼容性）
        /// </summary>
        public Dictionary<string, string> Environment
        {
            get => EnvironmentVariables;
            set => EnvironmentVariables = value ?? new Dictionary<string, string>();
        }

        /// <summary>
        /// 日志路径
        /// </summary>
        public string LogPath
        {
            get => LogDirectory;
            set
            {
                // LogPath 是 LogDirectory 的别名，所以我们需要小心处理
                // 如果设置值，我们需要确保它指向正确的目录
                // 在实际应用中，这个属性主要用于导出和兼容性
                // 这里我们不修改 LogDirectory，因为它是计算属性
            }
        }

        /// <summary>
        /// 日志模式
        /// </summary>
        public string LogMode { get; set; } = "roll-by-size";

        /// <summary>
        /// 启动模式
        /// </summary>
        public ServiceStartupMode StartMode { get; set; } = ServiceStartupMode.Automatic;

        /// <summary>
        /// 停止超时时间（毫秒）
        /// </summary>
        public int StopTimeout { get; set; } = 15000;

        /// <summary>
        /// 进程优先级
        /// </summary>
        public ProcessPriority Priority { get; set; } = ProcessPriority.Normal;

        /// <summary>
        /// 进程亲和性
        /// </summary>
        public int? Affinity { get; set; }

        /// <summary>
        /// 元数据
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// 最后一次启动结果
        /// </summary>
        public StartupResult LastStartupResult { get; set; } = StartupResult.Unknown;

        /// <summary>
        /// 最后一次启动错误消息
        /// </summary>
        public string? LastStartupErrorMessage { get; set; }

        /// <summary>
        /// 最后一次启动时间
        /// </summary>
        public DateTime? LastStartupTime { get; set; }

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
        /// 生成 WinSW 配置文件内容（安全的 XML 生成，防止注入）
        /// </summary>
        public string GenerateWinSWConfig()
        {
            try
            {
                var serviceElement = new XElement("service",
                    new XElement("id", SecurityElement.Escape(Id)),
                    new XElement("name", SecurityElement.Escape(DisplayName)),
                    new XElement("description", SecurityElement.Escape(Description)),
                    new XElement("executable", SecurityElement.Escape(ExecutablePath)),
                    new XElement("arguments", new XCData(GetFullArguments())),
                    new XElement("workingdirectory", SecurityElement.Escape(WorkingDirectory)),
                    new XElement("log", new XAttribute("mode", LogMode),
                        new XElement("sizeThreshold", 10240),
                        new XElement("keepFiles", 8)
                    ),
                    new XElement("stopparentprocessfirst", true)
                );

                // 添加依赖服务
                if (Dependencies.Any())
                {
                    var dependenciesElement = new XElement("dependencies");
                    foreach (var dependency in Dependencies)
                    {
                        dependenciesElement.Add(new XElement("service", SecurityElement.Escape(dependency)));
                    }
                    serviceElement.Add(dependenciesElement);
                }

                // 添加环境变量
                if (EnvironmentVariables.Any())
                {
                    var envElement = new XElement("env");
                    foreach (var envVar in EnvironmentVariables)
                    {
                        envElement.Add(new XElement("variable",
                            new XAttribute("name", SecurityElement.Escape(envVar.Key)),
                            new XAttribute("value", SecurityElement.Escape(envVar.Value))
                        ));
                    }
                    serviceElement.Add(envElement);
                }

                // 添加服务账户
                if (!string.IsNullOrEmpty(ServiceAccount))
                {
                    serviceElement.Add(new XElement("serviceaccount",
                        new XAttribute("accountName", SecurityElement.Escape(ServiceAccount))
                    ));
                }

                // 添加启动模式
                if (StartMode != ServiceStartupMode.Automatic)
                {
                    serviceElement.Add(new XElement("startmode", SecurityElement.Escape(StartMode.ToString().ToLower())));
                }

                // 添加停止超时
                serviceElement.Add(new XElement("stoptimeout", StopTimeout));

                var doc = new XDocument(
                    new XDeclaration("1.0", "utf-8", null),
                    serviceElement
                );

                return doc.ToString();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to generate WinSW configuration", ex);
            }
        }
    }

    /// <summary>
    /// 服务启动模式枚举
    /// </summary>
    public enum ServiceStartupMode
    {
        Automatic,
        Manual,
        Disabled
    }

    /// <summary>
    /// 进程优先级枚举
    /// </summary>
    public enum ProcessPriority
    {
        Idle,
        BelowNormal,
        Normal,
        AboveNormal,
        High,
        RealTime
    }
}