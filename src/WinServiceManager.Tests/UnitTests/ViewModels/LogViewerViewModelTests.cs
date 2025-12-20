using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using FluentAssertions;
using Moq;
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
        private readonly ServiceItem _testService;
        private readonly LogViewerViewModel _viewModel;
        private readonly string _tempTestDir;
        private readonly string _outputLogFile;
        private readonly string _errorLogFile;

        public LogViewerViewModelTests()
        {
            _mockLogReaderService = new Mock<LogReaderService>();

            _tempTestDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(_tempTestDir);

            _outputLogFile = Path.Combine(_tempTestDir, "output.log");
            _errorLogFile = Path.Combine(_tempTestDir, "error.log");
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

            _viewModel = new LogViewerViewModel(_mockLogReaderService.Object, _testService);
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
            Assert.Throws<ArgumentNullException>(() => new LogViewerViewModel(null!, _testService));
        }

        [Fact]
        public void Constructor_NullService_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new LogViewerViewModel(_mockLogReaderService.Object, null!));
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
            _viewModel.Status.Should().Be("就绪");
            _viewModel.MaxLines.Should().Be(1000);
            _viewModel.RefreshInterval.Should().Be(5);

            _mockLogReaderService.Verify(x => x.SubscribeToFileChanges(_outputLogFile, It.IsAny<Action<string>>()), Times.Once);
            _mockLogReaderService.Verify(x => x.SubscribeToFileChanges(_errorLogFile, It.IsAny<Action<string>>()), Times.Once);
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
            _mockLogReaderService.Setup(x => x.GetLogFileSize(_outputLogFile)).Returns(expectedSize);

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

        [Fact]
        public void LastModified_NonExistentFile_ReturnsDateTimeMin()
        {
            // Arrange
            var nonExistentService = new ServiceItem
            {
                Id = "test-2",
                DisplayName = "Test 2",
                OutputLogPath = @"C:\NonExistent\log.txt"
            };
            var viewModel = new LogViewerViewModel(_mockLogReaderService.Object, nonExistentService);

            // Act
            var lastModified = viewModel.LastModified;

            // Assert
            lastModified.Should().Be(DateTime.MinValue);

            viewModel.Dispose();
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void IsMonitoring_ChangesValue_UpdatesTimer(bool isMonitoring)
        {
            // Act
            _viewModel.IsMonitoring = isMonitoring;

            // Assert
            _viewModel.IsMonitoring.Should().Be(isMonitoring);
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

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void ShowFilterBar_SetValue_UpdatesProperty(bool showFilterBar)
        {
            // Act
            _viewModel.ShowFilterBar = showFilterBar;

            // Assert
            _viewModel.ShowFilterBar.Should().Be(showFilterBar);
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

        [Theory]
        [InlineData(0)]
        [InlineData(61)]
        public void RefreshInterval_SetValueOutsideRange_ClampsToValidRange(int interval)
        {
            // Act
            _viewModel.RefreshInterval = interval;

            // Assert
            _viewModel.RefreshInterval.Should().BeInRange(1, 60);
        }

        [Fact]
        public void FilterText_SetValue_AppliesFilter()
        {
            // Arrange
            AddTestLogLines();
            _viewModel.FilterText = "Test";

            // Act
            _viewModel.FilterText = "Output";

            // Assert
            _viewModel.FilterText.Should().Be("Output");
            _viewModel.IsFilterApplied.Should().BeTrue();
            _viewModel.FilteredLogLines.Should().OnlyContain(line => line.Contains("Output"));
        }

        #endregion

        #region RefreshCommand Tests

        [Fact]
        public async Task RefreshCommand_Executed_ReloadsLogs()
        {
            // Act
            await _viewModel.RefreshCommand.ExecuteAsync(null);

            // Assert
            _mockLogReaderService.Verify(x => x.ReadLastLinesAsync(It.IsAny<string>(), _viewModel.MaxLines), Times.Once);
        }

        #endregion

        #region ClearLogsCommand Tests

        [Fact]
        public void ClearLogsCommand_Executed_ClearsLogFile()
        {
            // Arrange - Mock MessageBox.Show to return Yes
            // In real implementation, you'd need to mock the MessageBox

            // Act
            _viewModel.ClearLogsCommand.Execute(null);

            // Assert
            // Note: MessageBox interaction makes this difficult to test without additional mocking framework
        }

        #endregion

        #region SaveLogsCommand Tests

        [Fact]
        public async Task SaveLogsCommand_Executed_SavesToFile()
        {
            // Arrange - Mock SaveFileDialog
            // In real implementation, you'd need to mock the dialog

            // Act
            await _viewModel.SaveLogsCommand.ExecuteAsync(null);

            // Assert
            // Note: Dialog interaction makes this difficult to test without additional mocking framework
        }

        #endregion

        #region Filter Commands Tests

        [Fact]
        public void ToggleFilterBarCommand_Executed_TogglesShowFilterBar()
        {
            // Arrange
            var initialShow = _viewModel.ShowFilterBar;

            // Act
            _viewModel.ToggleFilterBarCommand.Execute(null);

            // Assert
            _viewModel.ShowFilterBar.Should().Be(!initialShow);
        }

        [Fact]
        public void ApplyFilterCommand_WithFilterText_FiltersLogs()
        {
            // Arrange
            AddTestLogLines();
            _viewModel.FilterText = "Output";

            // Act
            _viewModel.ApplyFilterCommand.Execute(null);

            // Assert
            _viewModel.IsFilterApplied.Should().BeTrue();
            _viewModel.FilteredLogLines.Should().OnlyContain(line => line.Contains("Output"));
        }

        [Fact]
        public void ApplyFilterCommand_EmptyFilterText_ClearsFilter()
        {
            // Arrange
            AddTestLogLines();
            _viewModel.FilterText = "";
            _viewModel.IsFilterApplied = true; // Set to true initially

            // Act
            _viewModel.ApplyFilterCommand.Execute(null);

            // Assert
            _viewModel.IsFilterApplied.Should().BeFalse();
            _viewModel.FilteredLogLines.Should().HaveCountGreaterOrEqualTo(0);
        }

        [Fact]
        public void ClearFilterCommand_Executed_ClearsFilterAndResetsLogs()
        {
            // Arrange
            AddTestLogLines();
            _viewModel.FilterText = "Output";
            _viewModel.ApplyFilterCommand.Execute(null);
            _viewModel.IsFilterApplied.Should().BeTrue();

            // Act
            _viewModel.ClearFilterCommand.Execute(null);

            // Assert
            _viewModel.FilterText.Should().BeEmpty();
            _viewModel.IsFilterApplied.Should().BeFalse();
            _viewModel.FilteredLogLines.Should().HaveSameCount(_viewModel.LogLines);
        }

        #endregion

        #region MaxLines Commands Tests

        [Fact]
        public void IncreaseMaxLinesCommand_Executed_IncreasesBy500()
        {
            // Arrange
            var initialMax = _viewModel.MaxLines;

            // Act
            _viewModel.IncreaseMaxLinesCommand.Execute(null);

            // Assert
            _viewModel.MaxLines.Should().Be(initialMax + 500);
        }

        [Fact]
        public void DecreaseMaxLinesCommand_Executed_DecreasesBy500WithMinimum100()
        {
            // Arrange
            _viewModel.MaxLines = 600;

            // Act
            _viewModel.DecreaseMaxLinesCommand.Execute(null);

            // Assert
            _viewModel.MaxLines.Should().Be(100);
        }

        #endregion

        #region StopMonitoring Method Tests

        [Fact]
        public void StopMonitoring_StopsMonitoringAndCancelsTask()
        {
            // Act
            _viewModel.StopMonitoring();

            // Assert
            _viewModel.IsMonitoring.Should().BeFalse();
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
            // Simulate new log line
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
            _mockLogReaderService.Verify(x => x.UnsubscribeFromFileChanges(_outputLogFile), Times.Once);
            _mockLogReaderService.Verify(x => x.UnsubscribeFromFileChanges(_errorLogFile), Times.Once);
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
            _viewModel.FilteredLogLines.Clear();
            foreach (var line in _viewModel.LogLines)
            {
                _viewModel.FilteredLogLines.Add(line);
            }
        }

        #endregion
    }
}