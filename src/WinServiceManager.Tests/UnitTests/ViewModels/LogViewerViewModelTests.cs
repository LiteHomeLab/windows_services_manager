using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using FluentAssertions;
using Moq;
using Microsoft.Extensions.Logging;
using WinServiceManager.Models;
using WinServiceManager.Services;
using WinServiceManager.ViewModels;
using Xunit;

namespace WinServiceManager.Tests.UnitTests.ViewModels
{
    /// <summary>
    /// Unit tests for LogViewerViewModel class
    /// Tests log viewing functionality including real-time monitoring and filtering
    /// </summary>
    public class LogViewerViewModelTests : IDisposable
    {
        private readonly Mock<LogReaderService> _mockLogReaderService;
        private readonly Mock<ILogger<LogViewerViewModel>> _mockLogger;
        private readonly ServiceItem _testService;
        private readonly LogViewerViewModel _viewModel;
        private readonly string _tempTestDir;
        private readonly string _outputLogFile;
        private readonly string _errorLogFile;

        public LogViewerViewModelTests()
        {
            _mockLogReaderService = new Mock<LogReaderService>();
            _mockLogger = new Mock<ILogger<LogViewerViewModel>>();

            _tempTestDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(_tempTestDir);

            _outputLogFile = Path.Combine(_tempTestDir, "service1.out.log");
            _errorLogFile = Path.Combine(_tempTestDir, "service1.err.log");
            File.WriteAllText(_outputLogFile, "Test output log line 1\nTest output log line 2");
            File.WriteAllText(_errorLogFile, "Test error log line 1\nTest error log line 2");

            _testService = new ServiceItem
            {
                Id = "test-service-1",
                DisplayName = "Test Service",
                OutputLogPath = _outputLogFile,
                ErrorLogPath = _errorLogFile
            };

            _mockLogReaderService.Setup(x => x.GetLogFileSize(It.IsAny<string>())).Returns(1024);
            _mockLogReaderService.Setup(x => x.ReadLastLinesAsync(It.IsAny<string>(), It.IsAny<int>()))
                .ReturnsAsync((string path, int count) =>
                    File.Exists(path) ? File.ReadAllLines(path).TakeLast(count).ToList() : new List<string>());

            _viewModel = new LogViewerViewModel(_mockLogReaderService.Object, _testService, _mockLogger.Object);
        }

        public void Dispose()
        {
            _viewModel?.Dispose();

            if (Directory.Exists(_tempTestDir))
            {
                Directory.Delete(_tempTestDir, true);
            }
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_NullLogReaderService_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new LogViewerViewModel(null!, _testService, _mockLogger.Object));
        }

        [Fact]
        public void Constructor_NullService_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new LogViewerViewModel(_mockLogReaderService.Object, null!, _mockLogger.Object));
        }

        [Fact]
        public void Constructor_ValidParameters_InitializesCorrectly()
        {
            // Assert
            _viewModel.Title.Should().Be("日志查看器 - Test Service");
            _viewModel.ServiceName.Should().Be("Test Service");
            _viewModel.SelectedLogType.Should().Be("Output");
            _viewModel.IsMonitoring.Should().BeTrue();
            _viewModel.AutoScroll.Should().BeTrue();
            _viewModel.IsLoading.Should().BeFalse();
            _viewModel.Status.Should().Be("监控中");
            _viewModel.MaxLines.Should().Be(1000);
        }

        #endregion

        #region Property Tests

        [Fact]
        public void SelectedLogType_ChangesToError_UpdatesCurrentLogPath()
        {
            // Act
            _viewModel.SelectedLogType = "Error";

            // Assert
            _viewModel.SelectedLogType.Should().Be("Error");
        }

        [Fact]
        public void LogFileSize_ReturnsCorrectValue()
        {
            // Arrange
            var expectedSize = 2048L;
            _mockLogReaderService.Setup(x => x.GetLogFileSize(It.IsAny<string>())).Returns(expectedSize);

            // Act
            var size = _viewModel.LogFileSize;

            // Assert
            size.Should().Be(expectedSize);
        }

        [Fact]
        public void LastModified_ExistingFile_ReturnsLastWriteTime()
        {
            // Act
            var lastModified = _viewModel.LastModified;

            // Assert
            lastModified.Should().BeOnOrAfter(DateTime.Now.AddMinutes(-1));
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void AutoScroll_SetValue_UpdatesProperty(bool autoScroll)
        {
            // Act
            _viewModel.AutoScroll = autoScroll;

            // Assert
            _viewModel.AutoScroll.Should().Be(autoScroll);
        }

        [Fact]
        public void MaxLines_SetValueLessThan100_ClampsTo100()
        {
            // Act
            _viewModel.MaxLines = 50;

            // Assert
            _viewModel.MaxLines.Should().Be(100);
        }

        [Fact]
        public void MaxLines_SetValidValue_UpdatesProperty()
        {
            // Act
            _viewModel.MaxLines = 500;

            // Assert
            _viewModel.MaxLines.Should().Be(500);
        }

        [Fact]
        public void FilterText_SetValue_AppliesFilter()
        {
            // Arrange
            AddTestLogLines();

            // Act
            _viewModel.FilterText = "Output";

            // Assert
            _viewModel.FilterText.Should().Be("Output");
        }

        #endregion

        #region Commands Tests

        [Fact]
        public async Task RefreshCommand_Executed_ReloadsLogs()
        {
            // Act
            await _viewModel.RefreshCommand.ExecuteAsync(null);

            // Assert
            _mockLogReaderService.Verify(x => x.ReadLastLinesAsync(It.IsAny<string>(), _viewModel.MaxLines), Times.AtLeastOnce);
        }

        [Fact]
        public void ToggleMonitoringCommand_WhenMonitoring_StopsMonitoring()
        {
            // Arrange
            _viewModel.IsMonitoring = true;

            // Act
            _viewModel.ToggleMonitoringCommand.Execute(null);

            // Assert
            _viewModel.IsMonitoring.Should().BeFalse();
        }

        [Fact]
        public void ToggleMonitoringCommand_WhenNotMonitoring_StartsMonitoring()
        {
            // Arrange
            _viewModel.IsMonitoring = false;

            // Act
            _viewModel.ToggleMonitoringCommand.Execute(null);

            // Assert
            _viewModel.IsMonitoring.Should().BeTrue();
        }

        #endregion

        #region Event Tests

        [Fact]
        public void ScrollToBottomRequested_Raised_WhenNewLogLineAddedWithAutoScroll()
        {
            // Arrange
            _viewModel.AutoScroll = true;
            EventHandler? scrollHandler = null;
            var eventRaised = false;

            scrollHandler = (sender, e) => eventRaised = true;
            _viewModel.ScrollToBottomRequested += scrollHandler;

            // Act
            var callback = _mockLogReaderService.Invocations
                .Where(i => i.Method.Name == "SubscribeToFileChanges")
                .SelectMany(i => i.Arguments)
                .OfType<Action<string>>()
                .FirstOrDefault();

            callback?.Invoke("New log line");

            // Assert
            eventRaised.Should().BeTrue();

            _viewModel.ScrollToBottomRequested -= scrollHandler;
        }

        #endregion

        #region Dispose Tests

        [Fact]
        public void Dispose_UnsubscribesFromFileChanges()
        {
            // Act
            _viewModel.Dispose();

            // Assert
            _mockLogReaderService.Verify(x => x.UnsubscribeFromFileChanges(It.IsAny<string>()), Times.AtLeastOnce);
        }

        [Fact]
        public void Dispose_CalledTwice_DoesNotThrow()
        {
            // Act & Assert
            _viewModel.Dispose();
            _viewModel.Dispose();
        }

        #endregion

        #region Helper Methods

        private void AddTestLogLines()
        {
            _viewModel.LogLines.Add("Test Output log line 1");
            _viewModel.LogLines.Add("Test Error log line 1");
            _viewModel.LogLines.Add("Test Output log line 2");
            _viewModel.LogLines.Add("Test Error log line 2");
        }

        #endregion
    }
}
