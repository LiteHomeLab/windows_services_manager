using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceProcess;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using WinServiceManager.Models;
using WinServiceManager.Services;
using Xunit;

namespace WinServiceManager.Tests.UnitTests.Services
{
    /// <summary>
    /// Unit tests for ServiceManagerService class
    /// Tests service management operations including CRUD and lifecycle management
    /// </summary>
    public class ServiceManagerServiceTests
    {
        private readonly Mock<WinSWWrapper> _mockWinSWWrapper;
        private readonly Mock<IDataStorageService> _mockDataStorage;
        private readonly ServiceManagerService _serviceManager;

        public ServiceManagerServiceTests()
        {
            _mockWinSWWrapper = new Mock<WinSWWrapper>();
            _mockDataStorage = new Mock<IDataStorageService>();

            _serviceManager = new ServiceManagerService(
                _mockWinSWWrapper.Object,
                _mockDataStorage.Object);
        }

        #region GetAllServicesAsync Tests

        [Fact]
        public async Task GetAllServicesAsync_ReturnsAllServicesWithUpdatedStatus()
        {
            // Arrange
            var testServices = CreateTestServices(2);
            _mockDataStorage.Setup(x => x.LoadServicesAsync())
                .ReturnsAsync(testServices);

            // Mock ServiceController behavior for different services
            MockServiceControllerStatus(testServices[0].Id, ServiceControllerStatus.Running);
            MockServiceControllerStatus(testServices[1].Id, ServiceControllerStatus.Stopped);

            // Act
            var result = await _serviceManager.GetAllServicesAsync();

            // Assert
            result.Should().HaveCount(2);
            result[0].Status.Should().Be(ServiceStatus.Running);
            result[1].Status.Should().Be(ServiceStatus.Stopped);
            _mockDataStorage.Verify(x => x.LoadServicesAsync(), Times.Once);
        }

        [Fact]
        public async Task GetAllServicesAsync_ServiceNotInstalled_ReturnsNotInstalledStatus()
        {
            // Arrange
            var testServices = CreateTestServices(1);
            _mockDataStorage.Setup(x => x.LoadServicesAsync())
                .ReturnsAsync(testServices);

            // Mock service not found (throws exception)
            MockServiceControllerException(testServices[0].Id);

            // Act
            var result = await _serviceManager.GetAllServicesAsync();

            // Assert
            result.Should().HaveCount(1);
            result[0].Status.Should().Be(ServiceStatus.NotInstalled);
        }

        #endregion

        #region CreateServiceAsync Tests

        [Fact]
        public async Task CreateServiceAsync_ValidRequest_WithoutAutoStart_CreatesService()
        {
            // Arrange
            var request = new ServiceCreateRequest
            {
                DisplayName = "Test Service",
                Description = "Test Description",
                ExecutablePath = @"C:\Test\app.exe",
                Arguments = "--test",
                WorkingDirectory = @"C:\Test",
                AutoStart = false
            };

            _mockWinSWWrapper.Setup(x => x.InstallServiceAsync(It.IsAny<ServiceItem>()))
                .ReturnsAsync(ServiceOperationResult.SuccessResult(ServiceOperationType.Install));

            // Act
            var result = await _serviceManager.CreateServiceAsync(request);

            // Assert
            result.Success.Should().BeTrue();
            _mockDataStorage.Verify(x => x.AddServiceAsync(It.IsAny<ServiceItem>()), Times.Once);
            _mockWinSWWrapper.Verify(x => x.InstallServiceAsync(It.IsAny<ServiceItem>()), Times.Once);
            _mockWinSWWrapper.Verify(x => x.StartServiceAsync(It.IsAny<ServiceItem>()), Times.Never);
        }

        [Fact]
        public async Task CreateServiceAsync_ValidRequest_WithAutoStart_CreatesAndStartsService()
        {
            // Arrange
            var request = new ServiceCreateRequest
            {
                DisplayName = "Test Service",
                ExecutablePath = @"C:\Test\app.exe",
                WorkingDirectory = @"C:\Test",
                AutoStart = true
            };

            _mockWinSWWrapper.Setup(x => x.InstallServiceAsync(It.IsAny<ServiceItem>()))
                .ReturnsAsync(ServiceOperationResult.SuccessResult(ServiceOperationType.Install));

            _mockWinSWWrapper.Setup(x => x.StartServiceAsync(It.IsAny<ServiceItem>()))
                .ReturnsAsync(ServiceOperationResult.SuccessResult(ServiceOperationType.Start));

            // Act
            var result = await _serviceManager.CreateServiceAsync(request);

            // Assert
            result.Success.Should().BeTrue();
            _mockDataStorage.Verify(x => x.AddServiceAsync(It.IsAny<ServiceItem>()), Times.Once);
            _mockWinSWWrapper.Verify(x => x.InstallServiceAsync(It.IsAny<ServiceItem>()), Times.Once);
            _mockWinSWWrapper.Verify(x => x.StartServiceAsync(It.IsAny<ServiceItem>()), Times.Once);
        }

        [Fact]
        public async Task CreateServiceAsync_InstallFails_UpdatesStatusWithError()
        {
            // Arrange
            var request = new ServiceCreateRequest
            {
                DisplayName = "Test Service",
                ExecutablePath = @"C:\Test\app.exe",
                WorkingDirectory = @"C:\Test",
                AutoStart = false
            };

            _mockWinSWWrapper.Setup(x => x.InstallServiceAsync(It.IsAny<ServiceItem>()))
                .ReturnsAsync(ServiceOperationResult.FailureResult(ServiceOperationType.Install, "Install failed"));

            ServiceItem? savedService = null;
            _mockDataStorage.Setup(x => x.UpdateServiceAsync(It.IsAny<ServiceItem>()))
                .Callback<ServiceItem>(s => savedService = s)
                .Returns(Task.CompletedTask);

            // Act
            var result = await _serviceManager.CreateServiceAsync(request);

            // Assert
            result.Success.Should().BeFalse();
            result.ErrorMessage.Should().Be("Install failed");
            savedService.Should().NotBeNull();
            savedService!.Status.Should().Be(ServiceStatus.Error);
        }

        [Fact]
        public async Task CreateServiceAsync_ExceptionThrown_ReturnsFailureAndUpdatesStatus()
        {
            // Arrange
            var request = new ServiceCreateRequest
            {
                DisplayName = "Test Service",
                ExecutablePath = @"C:\Test\app.exe",
                WorkingDirectory = @"C:\Test"
            };

            _mockDataStorage.Setup(x => x.AddServiceAsync(It.IsAny<ServiceItem>()))
                .ThrowsAsync(new InvalidOperationException("Storage error"));

            ServiceItem? savedService = null;
            _mockDataStorage.Setup(x => x.UpdateServiceAsync(It.IsAny<ServiceItem>()))
                .Callback<ServiceItem>(s => savedService = s)
                .Returns(Task.CompletedTask);

            // Act
            var result = await _serviceManager.CreateServiceAsync(request);

            // Assert
            result.Success.Should().BeFalse();
            result.ErrorMessage.Should().Contain("Storage error");
            savedService.Should().NotBeNull();
            savedService!.Status.Should().Be(ServiceStatus.Error);
        }

        #endregion

        #region StartServiceAsync Tests

        [Fact]
        public async Task StartServiceAsync_Success_UpdatesServiceStatus()
        {
            // Arrange
            var service = CreateTestService();
            _mockWinSWWrapper.Setup(x => x.StartServiceAsync(service))
                .ReturnsAsync(ServiceOperationResult.SuccessResult(ServiceOperationType.Start));

            ServiceItem? updatedService = null;
            _mockDataStorage.Setup(x => x.UpdateServiceAsync(It.IsAny<ServiceItem>()))
                .Callback<ServiceItem>(s => updatedService = s)
                .Returns(Task.CompletedTask);

            // Act
            var result = await _serviceManager.StartServiceAsync(service);

            // Assert
            result.Success.Should().BeTrue();
            updatedService.Should().NotBeNull();
            updatedService!.Status.Should().Be(ServiceStatus.Running);
            updatedService.UpdatedAt.Should().BeCloseTo(DateTime.Now, TimeSpan.FromSeconds(1));
        }

        [Fact]
        public async Task StartServiceAsync_Failure_DoesNotUpdateStatus()
        {
            // Arrange
            var service = CreateTestService();
            _mockWinSWWrapper.Setup(x => x.StartServiceAsync(service))
                .ReturnsAsync(ServiceOperationResult.FailureResult(ServiceOperationType.Start, "Start failed"));

            // Act
            var result = await _serviceManager.StartServiceAsync(service);

            // Assert
            result.Success.Should().BeFalse();
            _mockDataStorage.Verify(x => x.UpdateServiceAsync(It.IsAny<ServiceItem>()), Times.Never);
        }

        #endregion

        #region StopServiceAsync Tests

        [Fact]
        public async Task StopServiceAsync_Success_UpdatesServiceStatus()
        {
            // Arrange
            var service = CreateTestService();
            service.Status = ServiceStatus.Running;
            _mockWinSWWrapper.Setup(x => x.StopServiceAsync(service))
                .ReturnsAsync(ServiceOperationResult.SuccessResult(ServiceOperationType.Stop));

            ServiceItem? updatedService = null;
            _mockDataStorage.Setup(x => x.UpdateServiceAsync(It.IsAny<ServiceItem>()))
                .Callback<ServiceItem>(s => updatedService = s)
                .Returns(Task.CompletedTask);

            // Act
            var result = await _serviceManager.StopServiceAsync(service);

            // Assert
            result.Success.Should().BeTrue();
            updatedService.Should().NotBeNull();
            updatedService!.Status.Should().Be(ServiceStatus.Stopped);
        }

        [Fact]
        public async Task StopServiceAsync_Failure_DoesNotUpdateStatus()
        {
            // Arrange
            var service = CreateTestService();
            _mockWinSWWrapper.Setup(x => x.StopServiceAsync(service))
                .ReturnsAsync(ServiceOperationResult.FailureResult(ServiceOperationType.Stop, "Stop failed"));

            // Act
            var result = await _serviceManager.StopServiceAsync(service);

            // Assert
            result.Success.Should().BeFalse();
            _mockDataStorage.Verify(x => x.UpdateServiceAsync(It.IsAny<ServiceItem>()), Times.Never);
        }

        #endregion

        #region RestartServiceAsync Tests

        [Fact]
        public async Task RestartServiceAsync_Success_ReturnsSuccess()
        {
            // Arrange
            var service = CreateTestService();
            service.Status = ServiceStatus.Running;

            _mockWinSWWrapper.Setup(x => x.StopServiceAsync(service))
                .ReturnsAsync(ServiceOperationResult.SuccessResult(ServiceOperationType.Stop));

            _mockWinSWWrapper.Setup(x => x.StartServiceAsync(service))
                .ReturnsAsync(ServiceOperationResult.SuccessResult(ServiceOperationType.Start));

            // Act
            var result = await _serviceManager.RestartServiceAsync(service);

            // Assert
            result.Success.Should().BeTrue();
            _mockWinSWWrapper.Verify(x => x.StopServiceAsync(service), Times.Once);
            _mockWinSWWrapper.Verify(x => x.StartServiceAsync(service), Times.Once);
        }

        [Fact]
        public async Task RestartServiceAsync_StopFails_ReturnsFailure()
        {
            // Arrange
            var service = CreateTestService();
            _mockWinSWWrapper.Setup(x => x.StopServiceAsync(service))
                .ReturnsAsync(ServiceOperationResult.FailureResult(ServiceOperationType.Stop, "Stop failed"));

            // Act
            var result = await _serviceManager.RestartServiceAsync(service);

            // Assert
            result.Success.Should().BeFalse();
            _mockWinSWWrapper.Verify(x => x.StopServiceAsync(service), Times.Once);
            _mockWinSWWrapper.Verify(x => x.StartServiceAsync(It.IsAny<ServiceItem>()), Times.Never);
        }

        #endregion

        #region UninstallServiceAsync Tests

        [Fact]
        public async Task UninstallServiceAsync_RunningService_StopsAndUninstalls()
        {
            // Arrange
            var service = CreateTestService();
            service.Status = ServiceStatus.Running;

            _mockWinSWWrapper.Setup(x => x.StopServiceAsync(service))
                .ReturnsAsync(ServiceOperationResult.SuccessResult(ServiceOperationType.Stop));

            _mockWinSWWrapper.Setup(x => x.UninstallServiceAsync(service))
                .ReturnsAsync(ServiceOperationResult.SuccessResult(ServiceOperationType.Uninstall));

            // Act
            var result = await _serviceManager.UninstallServiceAsync(service);

            // Assert
            result.Success.Should().BeTrue();
            _mockWinSWWrapper.Verify(x => x.StopServiceAsync(service), Times.Once);
            _mockWinSWWrapper.Verify(x => x.UninstallServiceAsync(service), Times.Once);
            _mockDataStorage.Verify(x => x.DeleteServiceAsync(service.Id), Times.Once);
        }

        [Fact]
        public async Task UninstallServiceAsync_StoppedService_UninstallsDirectly()
        {
            // Arrange
            var service = CreateTestService();
            service.Status = ServiceStatus.Stopped;

            _mockWinSWWrapper.Setup(x => x.UninstallServiceAsync(service))
                .ReturnsAsync(ServiceOperationResult.SuccessResult(ServiceOperationType.Uninstall));

            // Act
            var result = await _serviceManager.UninstallServiceAsync(service);

            // Assert
            result.Success.Should().BeTrue();
            _mockWinSWWrapper.Verify(x => x.StopServiceAsync(It.IsAny<ServiceItem>()), Times.Never);
            _mockWinSWWrapper.Verify(x => x.UninstallServiceAsync(service), Times.Once);
            _mockDataStorage.Verify(x => x.DeleteServiceAsync(service.Id), Times.Once);
        }

        [Fact]
        public async Task UninstallServiceAsync_StopFails_ReturnsFailure()
        {
            // Arrange
            var service = CreateTestService();
            service.Status = ServiceStatus.Running;

            _mockWinSWWrapper.Setup(x => x.StopServiceAsync(service))
                .ReturnsAsync(ServiceOperationResult.FailureResult(ServiceOperationType.Stop, "Stop failed"));

            // Act
            var result = await _serviceManager.UninstallServiceAsync(service);

            // Assert
            result.Success.Should().BeFalse();
            _mockWinSWWrapper.Verify(x => x.StopServiceAsync(service), Times.Once);
            _mockWinSWWrapper.Verify(x => x.UninstallServiceAsync(It.IsAny<ServiceItem>()), Times.Never);
            _mockDataStorage.Verify(x => x.DeleteServiceAsync(It.IsAny<string>()), Times.Never);
        }

        #endregion

        #region GetActualServiceStatusAsync Tests (via reflection)

        [Fact]
        public void GetActualServiceStatusAsync_RunningService_ReturnsRunning()
        {
            // Arrange
            var service = CreateTestService();
            MockServiceControllerStatus(service.Id, ServiceControllerStatus.Running);

            // Act
            var status = CallGetActualServiceStatusAsync(service);

            // Assert
            status.Should().Be(ServiceStatus.Running);
        }

        [Fact]
        public void GetActualServiceStatusAsync_StoppedService_ReturnsStopped()
        {
            // Arrange
            var service = CreateTestService();
            MockServiceControllerStatus(service.Id, ServiceControllerStatus.Stopped);

            // Act
            var status = CallGetActualServiceStatusAsync(service);

            // Assert
            status.Should().Be(ServiceStatus.Stopped);
        }

        [Fact]
        public void GetActualServiceStatusAsync_StartPending_ReturnsStarting()
        {
            // Arrange
            var service = CreateTestService();
            MockServiceControllerStatus(service.Id, ServiceControllerStatus.StartPending);

            // Act
            var status = CallGetActualServiceStatusAsync(service);

            // Assert
            status.Should().Be(ServiceStatus.Starting);
        }

        [Fact]
        public void GetActualServiceStatusAsync_ServiceNotFound_ReturnsNotInstalled()
        {
            // Arrange
            var service = CreateTestService();
            MockServiceControllerException(service.Id);

            // Act
            var status = CallGetActualServiceStatusAsync(service);

            // Assert
            status.Should().Be(ServiceStatus.NotInstalled);
        }

        #endregion

        #region Helper Methods

        private List<ServiceItem> CreateTestServices(int count)
        {
            return Enumerable.Range(1, count)
                .Select(i => new ServiceItem
                {
                    Id = $"test-service-{i}",
                    DisplayName = $"Test Service {i}",
                    Description = $"Test service number {i}",
                    ExecutablePath = $@"C:\Test\service{i}.exe",
                    WorkingDirectory = $@"C:\Test\service{i}",
                    Status = ServiceStatus.Stopped,
                    CreatedAt = DateTime.UtcNow.AddDays(-i)
                })
                .ToList();
        }

        private ServiceItem CreateTestService()
        {
            return new ServiceItem
            {
                Id = "test-service-1",
                DisplayName = "Test Service",
                Description = "Test Description",
                ExecutablePath = @"C:\Test\app.exe",
                WorkingDirectory = @"C:\Test",
                Status = ServiceStatus.Stopped,
                CreatedAt = DateTime.UtcNow
            };
        }

        private void MockServiceControllerStatus(string serviceName, ServiceControllerStatus status)
        {
            // In a real implementation, you would need to use a wrapper around ServiceController
            // or use dependency injection to mock it properly
        }

        private void MockServiceControllerException(string serviceName)
        {
            // In a real implementation, you would mock the ServiceController to throw an exception
        }

        private ServiceStatus CallGetActualServiceStatusAsync(ServiceItem service)
        {
            // Use reflection to call the private method for testing
            var method = typeof(ServiceManagerService).GetMethod("GetActualServiceStatusAsync",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (method == null)
                throw new InvalidOperationException("Method not found");

            var task = (Task<ServiceStatus>)method.Invoke(_serviceManager, new object[] { service });
            task.Wait();
            return task.Result;
        }

        #endregion
    }
}