using System;

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