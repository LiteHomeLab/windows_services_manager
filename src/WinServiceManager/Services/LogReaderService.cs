using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using WinServiceManager.Models;

namespace WinServiceManager.Services
{
    public class LogReaderService
    {
        public async Task<List<LogEntry>> ReadLogsAsync(string logPath, int maxLines = 1000)
        {
            var logs = new List<LogEntry>();

            if (!File.Exists(logPath))
                return logs;

            try
            {
                var lines = await File.ReadAllLinesAsync(logPath);

                // 从最后开始读取，读取最新的日志
                for (int i = Math.Max(0, lines.Length - maxLines); i < lines.Length; i++)
                {
                    var line = lines[i];
                    var entry = ParseLogLine(line);
                    if (entry != null)
                    {
                        logs.Add(entry);
                    }
                }
            }
            catch (Exception ex)
            {
                // 记录错误日志
                logs.Add(new LogEntry
                {
                    Timestamp = DateTime.Now,
                    Level = LogLevel.Error,
                    Message = $"读取日志失败: {ex.Message}",
                    ProcessId = 0,
                    RawLine = string.Empty
                });
            }

            return logs;
        }

        private LogEntry? ParseLogLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return null;

            // 简单的日志解析逻辑
            // 可以根据实际日志格式进行调整
            return new LogEntry
            {
                Timestamp = DateTime.Now,
                Level = LogLevel.Info,
                Message = line,
                ProcessId = 0,
                RawLine = line
            };
        }
    }
}