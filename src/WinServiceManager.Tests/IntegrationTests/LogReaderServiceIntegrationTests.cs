using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using WinServiceManager.Models;
using WinServiceManager.Services;
using Xunit;
using Xunit.Abstractions;

namespace WinServiceManager.Tests.IntegrationTests
{
    /// <summary>
    /// Integration tests for LogReaderService
    /// Tests real file operations and monitoring scenarios
    /// </summary>
    public class LogReaderServiceIntegrationTests : IDisposable
    {
        private readonly LogReaderService _logReaderService;
        private readonly string _testDirectory;
        private readonly List<string> _testFiles = new();
        private readonly ITestOutputHelper _output;

        public LogReaderServiceIntegrationTests(ITestOutputHelper output)
        {
            _output = output;
            _logReaderService = new LogReaderService();
            _testDirectory = Path.Combine(Path.GetTempPath(), "LogReaderServiceIntegrationTests", Guid.NewGuid().ToString());
            Directory.CreateDirectory(_testDirectory);
            _output.WriteLine($"Test directory: {_testDirectory}");
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
                catch (Exception ex)
                {
                    _output.WriteLine($"Failed to delete file {file}: {ex.Message}");
                }
            }

            try
            {
                if (Directory.Exists(_testDirectory))
                {
                    Directory.Delete(_testDirectory, true);
                }
            }
            catch (Exception ex)
            {
                _output.WriteLine($"Failed to delete directory {_testDirectory}: {ex.Message}");
            }
        }

        [Fact]
        public async Task RealTimeLogMonitoring_MultipleSubscribers_AllReceiveUpdates()
        {
            // Arrange
            var logFile = CreateTestLogFile("realtime.log", new[] { "Initial line" });
            var receivedLines1 = new List<string>();
            var receivedLines2 = new List<string>();
            var receivedLines3 = new List<string>();

            // Act - Subscribe multiple callbacks
            _logReaderService.SubscribeToFileChanges(logFile, line => receivedLines1.Add(line));
            _logReaderService.SubscribeToFileChanges(logFile, line => receivedLines2.Add(line));
            _logReaderService.SubscribeToFileChanges(logFile, line => receivedLines3.Add(line));

            // Allow watcher to initialize
            await Task.Delay(500);

            // Write multiple lines to file
            var linesToWrite = new[]
            {
                "Line 1: Starting process",
                "Line 2: Process running",
                "Line 3: Process completed"
            };

            foreach (var line in linesToWrite)
            {
                File.AppendAllText(logFile, Environment.NewLine + line);
                await Task.Delay(100); // Allow watcher to process each line
            }

            // Allow processing time
            await Task.Delay(1000);

            // Assert - All subscribers should receive all lines
            receivedLines1.Should().BeEquivalentTo(linesToWrite);
            receivedLines2.Should().BeEquivalentTo(linesToWrite);
            receivedLines3.Should().BeEquivalentTo(linesToWrite);

            // Cleanup
            _logReaderService.UnsubscribeFromFileChanges(logFile);
        }

        [Fact]
        public async Task FileRotation_WhenFileIsRecreated_ContinuesMonitoring()
        {
            // Arrange
            var logFile = CreateTestLogFile("rotate.log", new[] { "Original content" });
            var receivedLines = new List<string>();

            _logReaderService.SubscribeToFileChanges(logFile, line => receivedLines.Add(line));
            await Task.Delay(500);

            // Act - Simulate log rotation (delete and recreate file)
            File.Delete(logFile);
            await Task.Delay(100);

            File.WriteAllText(logFile, "Rotated content - Line 1");
            await Task.Delay(100);

            File.AppendAllText(logFile, Environment.NewLine + "Rotated content - Line 2");
            await Task.Delay(500);

            // Assert
            receivedLines.Should().Contain("Rotated content - Line 1");
            receivedLines.Should().Contain("Rotated content - Line 2");

            // Cleanup
            _logReaderService.UnsubscribeFromFileChanges(logFile);
        }

        [Fact]
        public async Task ConcurrentFileAccess_MultipleThreadsReadingAndWriting_ShouldNotConflict()
        {
            // Arrange
            var logFile = CreateTestLogFile("concurrent.log", new[] { "Initial" });
            var writerTasks = new List<Task>();
            var readerTasks = new List<Task<string[]>>();
            var linesWritten = new List<string>();

            // Act - Create multiple writers and readers
            for (int i = 0; i < 5; i++)
            {
                int writerIndex = i;
                writerTasks.Add(Task.Run(async () =>
                {
                    for (int j = 0; j < 10; j++)
                    {
                        var line = $"Writer {writerIndex} - Line {j}";
                        lock (linesWritten)
                        {
                            linesWritten.Add(line);
                        }
                        File.AppendAllText(logFile, Environment.NewLine + line);
                        await Task.Delay(50);
                    }
                }));
            }

            // Multiple readers reading last lines
            for (int i = 0; i < 3; i++)
            {
                readerTasks.Add(Task.Run(async () =>
                {
                    await Task.Delay(250); // Let writers start
                    return await _logReaderService.ReadLastLinesAsync(logFile, 20);
                }));
            }

            // Wait for all tasks to complete
            await Task.WhenAll(writerTasks);
            var readResults = await Task.WhenAll(readerTasks);

            // Assert
            linesWritten.Should().HaveCount(50); // 5 writers × 10 lines each
            readResults.Should().AllSatisfy(result =>
            {
                result.Should().NotBeNull();
                result.Length.Should().BeGreaterThan(0);
            });

            // Cleanup
            _logReaderService.UnsubscribeFromFileChanges(logFile);
        }

        [Fact]
        public async Task LargeLogFile_PerformanceTest_ShouldHandleEfficiently()
        {
            // Arrange
            var logFile = Path.Combine(_testDirectory, "large.log");
            const int lineCount = 100000;
            const int bytesPerLine = 100;

            // Create a large log file
            using (var writer = new StreamWriter(logFile))
            {
                for (int i = 0; i < lineCount; i++)
                {
                    var line = $"Line {i:D6}: " + new string('x', bytesPerLine - 15);
                    await writer.WriteLineAsync(line);
                }
            }
            _testFiles.Add(logFile);

            // Act - Test reading performance
            var startTime = DateTime.UtcNow;
            var lastLines = await _logReaderService.ReadLastLinesAsync(logFile, 1000);
            var readTime = DateTime.UtcNow - startTime;

            var fileSize = _logReaderService.GetLogFileSize(logFile);

            // Assert
            lastLines.Should().HaveCount(1000);
            lastLines.First().Should().Contain($"Line {lineCount - 1000:D6}");
            lastLines.Last().Should().Contain($"Line {lineCount - 1:D6}");
            fileSize.Should().BeGreaterThan(lineCount * bytesPerLine * 0.8);

            // Reading should be fast even for large files
            readTime.TotalSeconds.Should().BeLessThan(2);

            _output.WriteLine($"Read {lineCount:N0} lines ({fileSize:N0} bytes) in {readTime.TotalMilliseconds:N0}ms");
        }

        [Fact]
        public async Task ClearLogWhileMonitoring_ShouldContinueWorking()
        {
            // Arrange
            var logFile = CreateTestLogFile("clear-test.log", new[] { "Initial line" });
            var receivedLines = new List<string>();

            _logReaderService.SubscribeToFileChanges(logFile, line => receivedLines.Add(line));
            await Task.Delay(500);

            // Act
            // Write some lines
            File.AppendAllText(logFile, Environment.NewLine + "Line before clear");
            await Task.Delay(200);

            // Clear the log
            var clearResult = await _logReaderService.ClearLogFileAsync(logFile);
            await Task.Delay(200);

            // Write more lines after clear
            File.AppendAllText(logFile, "Line after clear 1");
            await Task.Delay(100);
            File.AppendAllText(logFile, Environment.NewLine + "Line after clear 2");
            await Task.Delay(500);

            // Assert
            clearResult.Should().BeTrue();
            receivedLines.Should().Contain("Line before clear");
            receivedLines.Should().Contain("Line after clear 1");
            receivedLines.Should().Contain("Line after clear 2");

            // File should be empty except for the lines written after clear
            var allLines = await File.ReadAllLinesAsync(logFile);
            allLines.Should().HaveCount(2);
            allLines[0].Should().Be("Line after clear 1");
            allLines[1].Should().Be("Line after clear 2");

            // Cleanup
            _logReaderService.UnsubscribeFromFileChanges(logFile);
        }

        [Fact]
        public async Task MultipleFilesMonitoring_ConcurrentMonitoring_ShouldWorkIndependently()
        {
            // Arrange
            var file1 = CreateTestLogFile("multi1.log", new[] { "File1 initial" });
            var file2 = CreateTestLogFile("multi2.log", new[] { "File2 initial" });
            var file3 = CreateTestLogFile("multi3.log", new[] { "File3 initial" });

            var file1Lines = new List<string>();
            var file2Lines = new List<string>();
            var file3Lines = new List<string>();

            // Act
            _logReaderService.SubscribeToFileChanges(file1, line => file1Lines.Add(line));
            _logReaderService.SubscribeToFileChanges(file2, line => file2Lines.Add(line));
            _logReaderService.SubscribeToFileChanges(file3, line => file3Lines.Add(line));

            await Task.Delay(500);

            // Write to all files concurrently
            var tasks = new[]
            {
                Task.Run(() => { File.AppendAllText(file1, Environment.NewLine + "File1 new line"); }),
                Task.Run(() => { File.AppendAllText(file2, Environment.NewLine + "File2 new line"); }),
                Task.Run(() => { File.AppendAllText(file3, Environment.NewLine + "File3 new line"); })
            };

            await Task.WhenAll(tasks);
            await Task.Delay(1000);

            // Assert
            file1Lines.Should().Contain("File1 new line");
            file2Lines.Should().Contain("File2 new line");
            file3Lines.Should().Contain("File3 new line");

            // Cross-contamination check
            file1Lines.Should().NotContain("File2 new line");
            file1Lines.Should().NotContain("File3 new line");

            // Cleanup
            _logReaderService.UnsubscribeFromFileChanges(file1);
            _logReaderService.UnsubscribeFromFileChanges(file2);
            _logReaderService.UnsubscribeFromFileChanges(file3);
        }

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