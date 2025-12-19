using System;
using System.IO;
using System.Security;
using System.Xml.Linq;

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
                    new XElement("arguments", SecurityElement.Escape(GetFullArguments())),
                    new XElement("workingdirectory", SecurityElement.Escape(WorkingDirectory)),
                    new XElement("log", new XAttribute("mode", "roll-by-size"),
                        new XElement("sizeThreshold", 10240),
                        new XElement("keepFiles", 8)
                    ),
                    new XElement("stopparentprocessfirst", true)
                );

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
}