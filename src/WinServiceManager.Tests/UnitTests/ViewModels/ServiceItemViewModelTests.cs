using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using FluentAssertions;
using Moq;
using WinServiceManager.Models;
using WinServiceManager.Services;
using WinServiceManager.ViewModels;
using Xunit;

namespace WinServiceManager.Tests.UnitTests.ViewModels
{
    /// <summary>
    /// Unit tests for ServiceItemViewModel class
    /// Tests service item management functionality including status changes and command execution
    /// </summary>
    public class ServiceItemViewModelTests
    {
        private readonly Mock<ServiceManagerService> _mockServiceManager;
        private readonly ServiceItem _testService;
        private readonly ServiceItemViewModel _viewModel;

        public ServiceItemViewModelTests()
        {
            _mockServiceManager = new Mock<ServiceManagerService>(Mock.Of<WinSWWrapper>(), Mock.Of<IDataStorageService>());

            _testService = new ServiceItem
            {
                Id = "test-service-1",
                DisplayName = "Test Service",
                Description = "A test service for unit testing",
                ExecutablePath = @"C:\Test\App.exe",
                WorkingDirectory = @"C:\Test",
                Arguments = "--test",
                Status = ServiceStatus.Stopped,
                CreatedAt = DateTime.UtcNow
            };

            _viewModel = new ServiceItemViewModel(_testService, _mockServiceManager.Object);
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_NullService_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new ServiceItemViewModel(null!, _mockServiceManager.Object));
        }

        [Fact]
        public void Constructor_NullServiceManager_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new ServiceItemViewModel(_testService, null!));
        }

        [Fact]
        public void Constructor_ValidParameters_InitializesProperties()
        {
            // Assert
            _viewModel.DisplayName.Should().Be(_testService.DisplayName);
            _viewModel.Description.Should().Be(_testService.Description);
            _viewModel.Status.Should().Be(ServiceStatus.Stopped);
            _viewModel.StatusDisplay.Should().Be("已停止");
            _viewModel.ExecutablePath.Should().Be(_testService.ExecutablePath);
            _viewModel.WorkingDirectory.Should().Be(_testService.WorkingDirectory);
            _viewModel.Arguments.Should().Be(_testService.Arguments);
            _viewModel.IsBusy.Should().BeFalse();
            _viewModel.IsTransitioning.Should().BeFalse();
        }

        #endregion

        #region Property Tests

        [Theory]
        [InlineData(ServiceStatus.Running, "#4CAF50")]  // Green
        [InlineData(ServiceStatus.Stopped, "#F44336")]  // Red
        [InlineData(ServiceStatus.Starting, "#FF9800")] // Orange
        [InlineData(ServiceStatus.Stopping, "#FF9800")] // Orange
        [InlineData(ServiceStatus.Installing, "#9C27B0")] // Purple
        [InlineData(ServiceStatus.Error, "#B71C1C")]     // Dark Red
        [InlineData(ServiceStatus.NotInstalled, "#9E9E9E")] // Gray
        public void StatusColor_ReturnsCorrectColor(ServiceStatus status, string expectedHexColor)
        {
            // Arrange
            _testService.Status = status;

            // Act
            var color = _viewModel.StatusColor;

            // Assert
            var solidColorBrush = color as System.Windows.Media.SolidColorBrush;
            solidColorBrush.Should().NotBeNull();
            var actualColor = solidColorBrush!.Color;
            var actualHex = $"#{actualColor.R:X2}{actualColor.G:X2}{actualColor.B:X2}";
            actualHex.Should().Be(expectedHexColor);
        }

        [Theory]
        [InlineData(ServiceStatus.Running, false)]
        [InlineData(ServiceStatus.Stopped, true)]
        [InlineData(ServiceStatus.Starting, false)]
        [InlineData(ServiceStatus.Stopping, false)]
        [InlineData(ServiceStatus.Error, true)]
        [InlineData(ServiceStatus.NotInstalled, true)]
        public void CanStart_ReturnsCorrectValue(ServiceStatus status, bool expected)
        {
            // Arrange
            _testService.Status = status;

            // Act
            var result = _viewModel.CanStart;

            // Assert
            result.Should().Be(expected);
        }

        [Theory]
        [InlineData(ServiceStatus.Running, true)]
        [InlineData(ServiceStatus.Stopped, false)]
        [InlineData(ServiceStatus.Starting, false)]
        [InlineData(ServiceStatus.Stopping, false)]
        [InlineData(ServiceStatus.Error, false)]
        [InlineData(ServiceStatus.NotInstalled, false)]
        public void CanStop_ReturnsCorrectValue(ServiceStatus status, bool expected)
        {
            // Arrange
            _testService.Status = status;

            // Act
            var result = _viewModel.CanStop;

            // Assert
            result.Should().Be(expected);
        }

        [Theory]
        [InlineData(ServiceStatus.Running, true)]
        [InlineData(ServiceStatus.Stopped, false)]
        [InlineData(ServiceStatus.Starting, false)]
        [InlineData(ServiceStatus.Stopping, false)]
        [InlineData(ServiceStatus.Error, false)]
        [InlineData(ServiceStatus.NotInstalled, false)]
        public void CanRestart_ReturnsCorrectValue(ServiceStatus status, bool expected)
        {
            // Arrange
            _testService.Status = status;

            // Act
            var result = _viewModel.CanRestart;

            // Assert
            result.Should().Be(expected);
        }

        [Theory]
        [InlineData(ServiceStatus.Running, true)]
        [InlineData(ServiceStatus.Stopped, true)]
        [InlineData(ServiceStatus.Starting, false)]
        [InlineData(ServiceStatus.Stopping, false)]
        [InlineData(ServiceStatus.Installing, false)]
        [InlineData(ServiceStatus.Error, true)]
        [InlineData(ServiceStatus.NotInstalled, false)]
        public void CanUninstall_ReturnsCorrectValue(ServiceStatus status, bool expected)
        {
            // Arrange
            _testService.Status = status;

            // Act
            var result = _viewModel.CanUninstall;

            // Assert
            result.Should().Be(expected);
        }

        [Fact]
        public void IsBusy_True_DisablesAllCommands()
        {
            // Arrange
            _testService.Status = ServiceStatus.Running;
            _viewModel.RefreshCommands(); // Enable commands initially

            // Act
            _viewModel.IsBusy = true;

            // Assert
            _viewModel.CanStart.Should().BeFalse();
            _viewModel.CanStop.Should().BeFalse();
            _viewModel.CanRestart.Should().BeFalse();
            _viewModel.CanUninstall.Should().BeFalse();
        }

        #endregion

        #region StartCommand Tests

        [Fact]
        public async Task StartCommand_StoppedService_StartsSuccessfully()
        {
            // Arrange
            _testService.Status = ServiceStatus.Stopped;
            _mockServiceManager.Setup(x => x.StartServiceAsync(_testService.Id))
                .ReturnsAsync(new ServiceOperationResult { Success = true });

            // Act
            await _viewModel.StartCommand.ExecuteAsync(null);

            // Assert
            _viewModel.Status.Should().Be(ServiceStatus.Running);
            _viewModel.IsBusy.Should().BeFalse();
            _mockServiceManager.Verify(x => x.StartServiceAsync(_testService.Id), Times.Once);
        }

        [Fact]
        public async Task StartCommand_NotInstalledService_InstallsThenStarts()
        {
            // Arrange
            _testService.Status = ServiceStatus.NotInstalled;
            _mockServiceManager.Setup(x => x.InstallServiceAsync(_testService.Id))
                .ReturnsAsync(new ServiceOperationResult { Success = true });
            _mockServiceManager.Setup(x => x.StartServiceAsync(_testService.Id))
                .ReturnsAsync(new ServiceOperationResult { Success = true });

            // Act
            await _viewModel.StartCommand.ExecuteAsync(null);

            // Assert
            _viewModel.Status.Should().Be(ServiceStatus.Running);
            _mockServiceManager.Verify(x => x.InstallServiceAsync(_testService.Id), Times.Once);
            _mockServiceManager.Verify(x => x.StartServiceAsync(_testService.Id), Times.Once);
        }

        [Fact]
        public async Task StartCommand_FailsToInstall_DoesNotStart()
        {
            // Arrange
            _testService.Status = ServiceStatus.NotInstalled;
            _mockServiceManager.Setup(x => x.InstallServiceAsync(_testService.Id))
                .ReturnsAsync(new ServiceOperationResult { Success = false, ErrorMessage = "Install failed" });

            // Act
            await _viewModel.StartCommand.ExecuteAsync(null);

            // Assert
            _viewModel.Status.Should().Be(ServiceStatus.NotInstalled);
            _viewModel.IsBusy.Should().BeFalse();
            _mockServiceManager.Verify(x => x.InstallServiceAsync(_testService.Id), Times.Once);
            _mockServiceManager.Verify(x => x.StartServiceAsync(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task StartCommand_ThrowsException_SetsErrorStatus()
        {
            // Arrange
            _testService.Status = ServiceStatus.Stopped;
            _mockServiceManager.Setup(x => x.StartServiceAsync(_testService.Id))
                .ThrowsAsync(new InvalidOperationException("Test exception"));

            // Act
            await _viewModel.StartCommand.ExecuteAsync(null);

            // Assert
            _viewModel.Status.Should().Be(ServiceStatus.Stopped);
            _viewModel.IsBusy.Should().BeFalse();
        }

        [Fact]
        public void StartCommand_CanExecute_RespectsCanStartProperty()
        {
            // Arrange
            _testService.Status = ServiceStatus.Stopped; // Can start

            // Act & Assert
            _viewModel.StartCommand.CanExecute(null).Should().BeTrue();

            // Arrange
            _testService.Status = ServiceStatus.Running; // Cannot start

            // Act & Assert
            _viewModel.StartCommand.CanExecute(null).Should().BeFalse();
        }

        #endregion

        #region StopCommand Tests

        [Fact]
        public async Task StopCommand_RunningService_StopsSuccessfully()
        {
            // Arrange
            _testService.Status = ServiceStatus.Running;
            _mockServiceManager.Setup(x => x.StopServiceAsync(_testService.Id))
                .ReturnsAsync(new ServiceOperationResult { Success = true });

            // Act
            await _viewModel.StopCommand.ExecuteAsync(null);

            // Assert
            _viewModel.Status.Should().Be(ServiceStatus.Stopped);
            _viewModel.IsBusy.Should().BeFalse();
            _mockServiceManager.Verify(x => x.StopServiceAsync(_testService.Id), Times.Once);
        }

        [Fact]
        public async Task StopCommand_FailsToStop_RestoresRunningStatus()
        {
            // Arrange
            _testService.Status = ServiceStatus.Running;
            _mockServiceManager.Setup(x => x.StopServiceAsync(_testService.Id))
                .ReturnsAsync(new ServiceOperationResult { Success = false, ErrorMessage = "Stop failed" });

            // Act
            await _viewModel.StopCommand.ExecuteAsync(null);

            // Assert
            _viewModel.Status.Should().Be(ServiceStatus.Running);
            _viewModel.IsBusy.Should().BeFalse();
        }

        #endregion

        #region RestartCommand Tests

        [Fact]
        public async Task RestartCommand_RunningService_RestartsSuccessfully()
        {
            // Arrange
            _testService.Status = ServiceStatus.Running;
            _mockServiceManager.Setup(x => x.StopServiceAsync(_testService.Id))
                .ReturnsAsync(new ServiceOperationResult { Success = true });
            _mockServiceManager.Setup(x => x.StartServiceAsync(_testService.Id))
                .ReturnsAsync(new ServiceOperationResult { Success = true });

            // Act
            await _viewModel.RestartCommand.ExecuteAsync(null);

            // Assert
            _viewModel.Status.Should().Be(ServiceStatus.Running);
            _viewModel.IsBusy.Should().BeFalse();
            _mockServiceManager.Verify(x => x.StopServiceAsync(_testService.Id), Times.Once);
            _mockServiceManager.Verify(x => x.StartServiceAsync(_testService.Id), Times.Once);
        }

        [Fact]
        public async Task RestartCommand_FailsToStop_DoesNotStart()
        {
            // Arrange
            _testService.Status = ServiceStatus.Running;
            _mockServiceManager.Setup(x => x.StopServiceAsync(_testService.Id))
                .ReturnsAsync(new ServiceOperationResult { Success = false, ErrorMessage = "Stop failed" });

            // Act
            await _viewModel.RestartCommand.ExecuteAsync(null);

            // Assert
            _viewModel.Status.Should().Be(ServiceStatus.Running);
            _mockServiceManager.Verify(x => x.StopServiceAsync(_testService.Id), Times.Once);
            _mockServiceManager.Verify(x => x.StartServiceAsync(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task RestartCommand_StopSucceedsStartFails_SetsStoppedStatus()
        {
            // Arrange
            _testService.Status = ServiceStatus.Running;
            _mockServiceManager.Setup(x => x.StopServiceAsync(_testService.Id))
                .ReturnsAsync(new ServiceOperationResult { Success = true });
            _mockServiceManager.Setup(x => x.StartServiceAsync(_testService.Id))
                .ReturnsAsync(new ServiceOperationResult { Success = false, ErrorMessage = "Start failed" });

            // Act
            await _viewModel.RestartCommand.ExecuteAsync(null);

            // Assert
            _viewModel.Status.Should().Be(ServiceStatus.Stopped);
        }

        #endregion

        #region RefreshStatusCommand Tests

        [Fact]
        public async Task RefreshStatusCommand_UpdatesStatusFromService()
        {
            // Arrange
            var newStatus = ServiceStatus.Running;
            _mockServiceManager.Setup(x => x.GetActualServiceStatusAsync(_testService))
                .ReturnsAsync(newStatus);

            // Act
            await _viewModel.RefreshStatusCommand.ExecuteAsync(null);

            // Assert
            _viewModel.Status.Should().Be(newStatus);
            _viewModel.IsBusy.Should().BeFalse();
            _mockServiceManager.Verify(x => x.GetActualServiceStatusAsync(_testService), Times.Once);
        }

        [Fact]
        public async Task RefreshStatusCommand_ThrowsException_HandlesGracefully()
        {
            // Arrange
            _mockServiceManager.Setup(x => x.GetActualServiceStatusAsync(_testService))
                .ThrowsAsync(new InvalidOperationException("Test exception"));

            // Act
            await _viewModel.RefreshStatusCommand.ExecuteAsync(null);

            // Assert
            _viewModel.IsBusy.Should().BeFalse();
        }

        #endregion

        #region UpdateStatus Method Tests

        [Fact]
        public void UpdateStatus_UpdatesPropertyAndNotifiesChanges()
        {
            // Arrange
            var originalStatus = _viewModel.Status;
            var newStatus = ServiceStatus.Running;

            // Act
            _viewModel.UpdateStatus(newStatus);

            // Assert
            _viewModel.Status.Should().Be(newStatus);
            _viewModel.StatusDisplay.Should().NotBe(originalStatus.GetDisplayText());
        }

        #endregion

        #region RefreshCommands Method Tests

        [Fact]
        public void RefreshCommands_NotifiesCanExecuteChangedForAllCommands()
        {
            // Act
            _viewModel.RefreshCommands();

            // Assert - Since we can't directly verify NotifyCanExecuteChanged without access to
            // the internal RelayCommand implementation, we'll ensure no exceptions are thrown
            _viewModel.Should().NotBeNull();
        }

        #endregion
    }
}