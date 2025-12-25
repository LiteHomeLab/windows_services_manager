using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;

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

        /// <summary>
        /// 依赖的服务ID列表
        /// </summary>
        public List<string> Dependencies { get; set; } = new();

        /// <summary>
        /// 环境变量键值对
        /// </summary>
        public Dictionary<string, string> EnvironmentVariables { get; set; } = new();

        /// <summary>
        /// 服务账户
        /// </summary>
        public string? ServiceAccount { get; set; }

        /// <summary>
        /// 启动模式
        /// </summary>
        public string StartMode { get; set; } = "Automatic";

        /// <summary>
        /// 停止超时时间（毫秒）
        /// </summary>
        public int StopTimeout { get; set; } = 15000;

        /// <summary>
        /// 启用退出码自动重启
        /// </summary>
        public bool EnableRestartOnExit { get; set; } = false;

        /// <summary>
        /// 触发重启的退出码
        /// </summary>
        public int RestartExitCode { get; set; } = 99;
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