using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using WinServiceManager.Models;
using WinServiceManager.Services;
using Xunit;

namespace WinServiceManager.Tests.UnitTests.Services
{
    /// <summary>
    /// Unit tests for LogReaderService class
    /// </summary>
    public class LogReaderServiceTests : IDisposable
    {
        private readonly LogReaderService _logReaderService;
        private readonly string _testDirectory;
        private readonly List<string> _testFiles = new();

        public LogReaderServiceTests()
        {
            _logReaderService = new LogReaderService();
            _testDirectory = Path.Combine(Path.GetTempPath(), "LogReaderServiceTests", Guid.NewGuid().ToString());
            Directory.CreateDirectory(_testDirectory);
        }

        public void Dispose()
        {
            _logReaderService?.Dispose();

            // Clean up test files
            foreach (var file in _testFiles)
            {
                try
                {
                    if (File.Exists(file))
                    {
                        File.Delete(file);
                    }
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }

            try
            {
                if (Directory.Exists(_testDirectory))
                {
                    Directory.Delete(_testDirectory, true);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        #region ReadLogsAsync Tests

        [Fact]
        public async Task ReadLogsAsync_WithNonExistentFile_ReturnsEmptyList()
        {
            // Arrange
            var nonExistentFile = Path.Combine(_testDirectory, "nonexistent.log");

            // Act
            var result = await _logReaderService.ReadLogsAsync(nonExistentFile);

            // Assert
            result.Should().NotBeNull();
            result.Should().BeEmpty();
        }

        [Fact]
        public async Task ReadLogsAsync_WithExistingFile_ReturnsLogEntries()
        {
            // Arrange
            var logFile = CreateTestLogFile("test.log", new[]
            {
                "2024-01-01 10:00:00 INFO Test log line 1",
                "2024-01-01 10:00:01 ERROR Test error line",
                "2024-01-01 10:00:02 WARNING Test warning line"
            });

            // Act
            var result = await _logReaderService.ReadLogsAsync(logFile, maxLines: 10);

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(3);
            result.Should().AllSatisfy(entry => entry.Should().NotBeNull());
        }

        [Fact]
        public async Task ReadLogsAsync_WithMaxLines_ReturnsLimitedEntries()
        {
            // Arrange
            var logFile = CreateTestLogFile("test.log", Enumerable.Range(1, 20)
                .Select(i => $"2024-01-01 10:00:{i:D2} INFO Log line {i}"));

            // Act
            var result = await _logReaderService.ReadLogsAsync(logFile, maxLines: 5);

            // Assert
            result.Should().HaveCount(5);
            result.Last().Message.Should().Contain("Log line 20"); // Should get the last 5 lines
        }

        #endregion

        #region ReadLastLinesAsync Tests

        [Fact]
        public async Task ReadLastLinesAsync_WithNonExistentFile_ReturnsEmptyArray()
        {
            // Arrange
            var nonExistentFile = Path.Combine(_testDirectory, "nonexistent.log");

            // Act
            var result = await _logReaderService.ReadLastLinesAsync(nonExistentFile, 10);

            // Assert
            result.Should().NotBeNull();
            result.Should().BeEmpty();
        }

        [Fact]
        public async Task ReadLastLinesAsync_WithExistingFile_ReturnsLastLines()
        {
            // Arrange
            var lines = Enumerable.Range(1, 100).Select(i => $"Line {i}");
            var logFile = CreateTestLogFile("test.log", lines);

            // Act
            var result = await _logReaderService.ReadLastLinesAsync(logFile, 10);

            // Assert
            result.Should().HaveCount(10);
            result.Should().ContainInOrder(lines.TakeLast(10));
        }

        [Fact]
        public async Task ReadLastLinesAsync_WithMaxLinesGreaterThanFile_ReturnsAllLines()
        {
            // Arrange
            var lines = new[] { "Line 1", "Line 2", "Line 3" };
            var logFile = CreateTestLogFile("test.log", lines);

            // Act
            var result = await _logReaderService.ReadLastLinesAsync(logFile, 10);

            // Assert
            result.Should().HaveCount(3);
            result.Should().ContainInOrder(lines);
        }

        #endregion

        #region GetLogFileSize Tests

        [Fact]
        public void GetLogFileSize_WithNonExistentFile_ReturnsZero()
        {
            // Arrange
            var nonExistentFile = Path.Combine(_testDirectory, "nonexistent.log");

            // Act
            var result = _logReaderService.GetLogFileSize(nonExistentFile);

            // Assert
            result.Should().Be(0);
        }

        [Fact]
        public void GetLogFileSize_WithExistingFile_ReturnsFileSize()
        {
            // Arrange
            var logFile = CreateTestLogFile("test.log", new[]
            {
                "Line 1",
                "Line 2",
                "Line 3"
            });

            // Act
            var result = _logReaderService.GetLogFileSize(logFile);

            // Assert
            result.Should().BeGreaterThan(0);
        }

        [Fact]
        public void GetLogFileSize_WithEmptyFile_ReturnsZero()
        {
            // Arrange
            var logFile = Path.Combine(_testDirectory, "empty.log");
            File.WriteAllText(logFile, string.Empty);
            _testFiles.Add(logFile);

            // Act
            var result = _logReaderService.GetLogFileSize(logFile);

            // Assert
            result.Should().Be(0);
        }

        #endregion

        #region ClearLogFileAsync Tests

        [Fact]
        public async Task ClearLogFileAsync_WithNonExistentFile_ReturnsFalse()
        {
            // Arrange
            var nonExistentFile = Path.Combine(_testDirectory, "nonexistent.log");

            // Act
            var result = await _logReaderService.ClearLogFileAsync(nonExistentFile);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task ClearLogFileAsync_WithExistingFile_ClearsFileAndReturnsTrue()
        {
            // Arrange
            var logFile = CreateTestLogFile("test.log", new[]
            {
                "Line 1",
                "Line 2",
                "Line 3"
            });

            // Act
            var result = await _logReaderService.ClearLogFileAsync(logFile);

            // Assert
            result.Should().BeTrue();
            var fileContent = await File.ReadAllTextAsync(logFile);
            fileContent.Should().BeEmpty();
        }

        #endregion

        #region File Monitoring Tests

        [Fact]
        public void SubscribeToFileChanges_WithValidPath_SubscribesSuccessfully()
        {
            // Arrange
            var logFile = CreateTestLogFile("test.log", new[] { "Initial line" });
            var callbackInvoked = false;
            Action<string> callback = _ => callbackInvoked = true;

            // Act
            _logReaderService.SubscribeToFileChanges(logFile, callback);

            // Write to file to trigger callback
            Thread.Sleep(100); // Allow watcher to initialize
            File.AppendAllText(logFile, "\nNew line");
            Thread.Sleep(200); // Allow watcher to detect change

            // Assert
            callbackInvoked.Should().BeTrue();

            // Cleanup
            _logReaderService.UnsubscribeFromFileChanges(logFile);
        }

        [Fact]
        public void UnsubscribeFromFileChanges_WithValidPath_UnsubscribesSuccessfully()
        {
            // Arrange
            var logFile = CreateTestLogFile("test.log", new[] { "Initial line" });
            var callbackCount = 0;
            Action<string> callback = _ => Interlocked.Increment(ref callbackCount);

            _logReaderService.SubscribeToFileChanges(logFile, callback);
            Thread.Sleep(100); // Allow watcher to initialize

            // Act
            _logReaderService.UnsubscribeFromFileChanges(logFile);
            Thread.Sleep(100); // Allow unsubscription to complete

            // Write to file after unsubscribe
            File.AppendAllText(logFile, "\nLine after unsubscribe");
            Thread.Sleep(200);

            // Assert
            callbackCount.Should().Be(0);
        }

        #endregion

        #region MonitorNewLinesAsync Tests

        [Fact]
        public async Task MonitorNewLinesAsync_WithCancellation_StopsMonitoring()
        {
            // Arrange
            var logFile = CreateTestLogFile("test.log", new[] { "Initial line" });
            var cts = new CancellationTokenSource();
            var callbackInvoked = false;
            Action<string> callback = _ => callbackInvoked = true;

            _logReaderService.SubscribeToFileChanges(logFile, callback);

            // Act
            var monitorTask = _logReaderService.MonitorNewLinesAsync(logFile, cts.Token);

            // Wait a bit then cancel
            await Task.Delay(100);
            cts.Cancel();

            // Assert
            var result = await monitorTask;
            result.Should().BeNull(); // Task should complete without exception

            // Cleanup
            _logReaderService.UnsubscribeFromFileChanges(logFile);
        }

        #endregion

        #region Error Handling Tests

        [Fact]
        public async Task ReadLogsAsync_WithInvalidCharacters_HandlesGracefully()
        {
            // Arrange
            var logFile = CreateTestLogFile("test.log", new[]
            {
                "Valid line",
                "\x00\x01\x02 Invalid characters",
                "Another valid line"
            });

            // Act
            var result = await _logReaderService.ReadLogsAsync(logFile);

            // Assert
            result.Should().NotBeNull();
            result.Should().NotBeEmpty();
            // All lines should be returned, even those with invalid characters
            result.Should().HaveCount(3);
        }

        [Fact]
        public async Task ReadLastLinesAsync_WithVeryLongFile_HandlesEfficiently()
        {
            // Arrange
            var lines = Enumerable.Range(1, 10000).Select(i => $"This is line {i:D5} with some content");
            var logFile = CreateTestLogFile("large.log", lines);

            // Act
            var startTime = DateTime.UtcNow;
            var result = await _logReaderService.ReadLastLinesAsync(logFile, 100);
            var endTime = DateTime.UtcNow;

            // Assert
            result.Should().HaveCount(100);
            result.Should().ContainInOrder(lines.TakeLast(100));
            // Should read quickly even for large files
            (endTime - startTime).TotalSeconds.Should().BeLessThan(1);
        }

        #endregion

        #region Helper Methods

        private string CreateTestLogFile(string fileName, IEnumerable<string> lines)
        {
            var filePath = Path.Combine(_testDirectory, fileName);
            File.WriteAllLines(filePath, lines);
            _testFiles.Add(filePath);
            return filePath;
        }

        #endregion
    }
}