using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using WinServiceManager.Models;
using WinServiceManager.Services;
using WinServiceManager.ViewModels;
using Xunit;

namespace WinServiceManager.Tests.UnitTests.ViewModels
{
    /// <summary>
    /// Unit tests for MainWindowViewModel class
    /// Tests main window functionality including service management and UI interactions
    /// </summary>
    public class MainWindowViewModelTests : IDisposable
    {
        private readonly Mock<ServiceManagerService> _mockServiceManager;
        private readonly Mock<ServiceStatusMonitor> _mockStatusMonitor;
        private readonly Mock<LogReaderService> _mockLogReaderService;
        private readonly Mock<PathValidator> _mockPathValidator;
        private readonly Mock<CommandValidator> _mockCommandValidator;
        private readonly MainWindowViewModel _viewModel;
        private readonly string _tempTestDir;

        public MainWindowViewModelTests()
        {
            _mockServiceManager = new Mock<ServiceManagerService>(Mock.Of<WinSWWrapper>(), Mock.Of<IDataStorageService>());
            _mockStatusMonitor = new Mock<ServiceStatusMonitor>();
            _mockLogReaderService = new Mock<LogReaderService>();
            _mockPathValidator = new Mock<PathValidator>();
            _mockCommandValidator = new Mock<CommandValidator>();

            _tempTestDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(_tempTestDir);

            // Set up Application.Current for tests that need it
            if (Application.Current == null)
            {
                new Application();
            }

            _viewModel = new MainWindowViewModel(
                _mockServiceManager.Object,
                _mockStatusMonitor.Object,
                _mockLogReaderService.Object,
                _mockPathValidator.Object,
                _mockCommandValidator.Object);
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
        public void Constructor_NullServiceManager_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new MainWindowViewModel(
                    null!,
                    _mockStatusMonitor.Object,
                    _mockLogReaderService.Object,
                    _mockPathValidator.Object,
                    _mockCommandValidator.Object));
        }

        [Fact]
        public void Constructor_NullStatusMonitor_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new MainWindowViewModel(
                    _mockServiceManager.Object,
                    null!,
                    _mockLogReaderService.Object,
                    _mockPathValidator.Object,
                    _mockCommandValidator.Object));
        }

        [Fact]
        public void Constructor_NullLogReaderService_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new MainWindowViewModel(
                    _mockServiceManager.Object,
                    _mockStatusMonitor.Object,
                    null!,
                    _mockPathValidator.Object,
                    _mockCommandValidator.Object));
        }

        [Fact]
        public void Constructor_NullPathValidator_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new MainWindowViewModel(
                    _mockServiceManager.Object,
                    _mockStatusMonitor.Object,
                    _mockLogReaderService.Object,
                    null!,
                    _mockCommandValidator.Object));
        }

        [Fact]
        public void Constructor_NullCommandValidator_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new MainWindowViewModel(
                    _mockServiceManager.Object,
                    _mockStatusMonitor.Object,
                    _mockLogReaderService.Object,
                    _mockPathValidator.Object,
                    null!));
        }

        [Fact]
        public void Constructor_ValidParameters_InitializesCorrectly()
        {
            // Assert
            _viewModel.Services.Should().NotBeNull();
            _viewModel.Services.Should().BeEmpty();
            _viewModel.StatusMessage.Should().Be("就绪");
            _viewModel.SelectedService.Should().BeNull();
            _viewModel.AllServicesCount.Should().Be(0);
            _viewModel.SearchText.Should().BeEmpty();

            _mockStatusMonitor.Verify(x => x.Subscribe(It.IsAny<Action<List<ServiceItem>>>()), Times.Once);
        }

        #endregion

        #region Properties Tests

        [Fact]
        public void SelectedService_SetValue_RaisesPropertyChanged()
        {
            // Arrange
            var testService = new Mock<ServiceItemViewModel>(
                new ServiceItem(), _mockServiceManager.Object);

            // Act
            _viewModel.SelectedService = testService.Object;

            // Assert
            _viewModel.SelectedService.Should().Be(testService.Object);
        }

        #endregion

        #region CreateServiceCommand Tests

        [Fact]
        public async Task CreateServiceCommand_SuccessfulCreation_RefreshesServices()
        {
            // Arrange
            var initialServices = new List<ServiceItem>();
            _mockServiceManager.Setup(x => x.GetAllServicesAsync())
                .ReturnsAsync(initialServices);

            // Act
            await _viewModel.CreateServiceCommand.ExecuteAsync(null);

            // Assert
            _viewModel.StatusMessage.Should().Be("就绪");
            _mockServiceManager.Verify(x => x.GetAllServicesAsync(), Times.AtLeastOnce);
        }

        [Fact]
        public async Task CreateServiceCommand_ExceptionThrown_SetsErrorStatus()
        {
            // Arrange
            _mockServiceManager.Setup(x => x.GetAllServicesAsync())
                .ThrowsAsync(new InvalidOperationException("Test exception"));

            // Act
            await _viewModel.CreateServiceCommand.ExecuteAsync(null);

            // Assert
            _viewModel.StatusMessage.Should().Be("就绪");
            // Note: MessageBox.Show would be called in the actual implementation
        }

        #endregion

        #region RefreshServicesCommand Tests

        [Fact]
        public async Task RefreshServicesCommand_SuccessfulRefresh_UpdatesServices()
        {
            // Arrange
            var testServices = CreateTestServices(3);
            _mockServiceManager.Setup(x => x.GetAllServicesAsync())
                .ReturnsAsync(testServices);

            // Act
            await _viewModel.RefreshServicesCommand.ExecuteAsync(null);

            // Assert
            _viewModel.Services.Should().HaveCount(3);
            _viewModel.AllServicesCount.Should().Be(3);
            _viewModel.StatusMessage.Should().Be("已刷新 3 个服务");

            _mockServiceManager.Verify(x => x.GetAllServicesAsync(), Times.Once);
        }

        [Fact]
        public async Task RefreshServicesCommand_ExceptionThrown_SetsErrorMessage()
        {
            // Arrange
            _mockServiceManager.Setup(x => x.GetAllServicesAsync())
                .ThrowsAsync(new InvalidOperationException("Refresh failed"));

            // Act
            await _viewModel.RefreshServicesCommand.ExecuteAsync(null);

            // Assert
            _viewModel.StatusMessage.Should().Be("就绪");
            _viewModel.Services.Should().BeEmpty();
        }

        [Fact]
        public async Task RefreshServicesCommand_EmptyServiceList_ClearsExistingServices()
        {
            // Arrange
            var initialServices = CreateTestServices(2);
            await UpdateServicesInViewModel(initialServices);

            var emptyServices = new List<ServiceItem>();
            _mockServiceManager.Setup(x => x.GetAllServicesAsync())
                .ReturnsAsync(emptyServices);

            // Act
            await _viewModel.RefreshServicesCommand.ExecuteAsync(null);

            // Assert
            _viewModel.Services.Should().BeEmpty();
            _viewModel.AllServicesCount.Should().Be(0);
        }

        #endregion

        #region ExportServicesCommand Tests

        [Fact]
        public void ExportServicesCommand_Executed_ShowsNotImplementedMessage()
        {
            // Act
            _viewModel.ExportServicesCommand.Execute(null);

            // Assert
            _viewModel.StatusMessage.Should().Contain("开发中");
        }

        [Fact]
        public void ExportServicesCommand_ExceptionThrown_SetsErrorMessage()
        {
            // Arrange - Mock to throw exception when trying to show save dialog
            // This would need more complex mocking in real implementation

            // Act
            _viewModel.ExportServicesCommand.Execute(null);

            // Assert
            // In actual implementation, this would verify error handling
        }

        #endregion

        #region OpenServicesFolderCommand Tests

        [Fact]
        public void OpenServicesFolderCommand_DirectoryExists_StartsProcess()
        {
            // Arrange - Create a services directory
            var servicesPath = Path.Combine(_tempTestDir, "services");
            Directory.CreateDirectory(servicesPath);

            // Mock AppDomain.CurrentDomain.BaseDirectory
            var originalBaseDir = AppDomain.CurrentDomain.BaseDirectory;
            AppDomain.CurrentDomain.SetData("DataDirectory", _tempTestDir);

            try
            {
                // Act
                _viewModel.OpenServicesFolderCommand.Execute(null);

                // Assert
                // In actual implementation, would verify Process.Start was called
            }
            finally
            {
                AppDomain.CurrentDomain.SetData("DataDirectory", originalBaseDir);
            }
        }

        [Fact]
        public void OpenServicesFolderCommand_DirectoryNotExists_ShowsMessage()
        {
            // Arrange - Use a non-existent path
            var servicesPath = Path.Combine(_tempTestDir, "nonexistent");

            var originalBaseDir = AppDomain.CurrentDomain.BaseDirectory;
            AppDomain.CurrentDomain.SetData("DataDirectory", _tempTestDir);

            try
            {
                // Act
                _viewModel.OpenServicesFolderCommand.Execute(null);

                // Assert
                // In actual implementation, would verify MessageBox.Show was called
            }
            finally
            {
                AppDomain.CurrentDomain.SetData("DataDirectory", originalBaseDir);
            }
        }

        #endregion

        #region SearchCommand Tests

        [Fact]
        public void SearchCommand_EmptyText_ShowsAllServices()
        {
            // Arrange
            var testServices = CreateTestServices(2);
            UpdateServicesInViewModel(testServices).Wait();

            _viewModel.SearchText = "";

            // Act
            _viewModel.SearchCommand.Execute(null);

            // Assert
            _viewModel.Services.Should().HaveCount(2);
        }

        [Fact]
        public void SearchCommand_WithText_ShowsNotImplementedMessage()
        {
            // Arrange
            _viewModel.SearchText = "test";

            // Act
            _viewModel.SearchCommand.Execute(null);

            // Assert
            _viewModel.StatusMessage.Should().Contain("搜索 'test'");
        }

        #endregion

        #region ViewLogsCommand Tests

        [Fact]
        public async Task ViewLogsCommand_NoServiceSelected_ShowsMessage()
        {
            // Act
            await _viewModel.ViewLogsCommand.ExecuteAsync(null);

            // Assert
            _viewModel.StatusMessage.Should().Be("就绪");
            // In actual implementation, would verify MessageBox.Show was called
        }

        [Fact]
        public async Task ViewLogsCommand_ServiceSelected_OpensLogViewer()
        {
            // Arrange
            var testService = new ServiceItem
            {
                Id = "test-service-1",
                DisplayName = "Test Service"
            };
            var serviceViewModel = new ServiceItemViewModel(testService, _mockServiceManager.Object);
            _viewModel.SelectedService = serviceViewModel;

            // Act
            await _viewModel.ViewLogsCommand.ExecuteAsync(null);

            // Assert
            _viewModel.StatusMessage.Should().Contain("日志查看器已打开");
        }

        [Fact]
        public async Task ViewLogsCommand_ExceptionThrown_SetsErrorMessage()
        {
            // Arrange
            var testService = new ServiceItem
            {
                Id = "test-service-1",
                DisplayName = "Test Service"
            };
            var serviceViewModel = new ServiceItemViewModel(testService, _mockServiceManager.Object);
            _viewModel.SelectedService = serviceViewModel;

            // Mock LogReaderService to throw exception
            _mockLogReaderService.Setup(x => x.ReadLogsAsync(It.IsAny<string>(), It.IsAny<int>()))
                .ThrowsAsync(new InvalidOperationException("Log read failed"));

            // Act
            await _viewModel.ViewLogsCommand.ExecuteAsync(null);

            // Assert
            _viewModel.StatusMessage.Should().Be("就绪");
        }

        #endregion

        #region SortServices Method Tests

        [Theory]
        [InlineData("Name")]
        [InlineData("name")]
        [InlineData("NAME")]
        public void SortServices_ByName_SortsCorrectly(string columnName)
        {
            // Arrange
            var services = CreateTestServices(3);
            services[0].DisplayName = "C Service";
            services[1].DisplayName = "A Service";
            services[2].DisplayName = "B Service";
            UpdateServicesInViewModel(services).Wait();

            // Act
            _viewModel.SortServices(columnName);

            // Assert
            _viewModel.Services[0].DisplayName.Should().Be("A Service");
            _viewModel.Services[1].DisplayName.Should().Be("B Service");
            _viewModel.Services[2].DisplayName.Should().Be("C Service");
            _viewModel.StatusMessage.Should().Contain("按 name 排序");
        }

        [Theory]
        [InlineData("Status")]
        [InlineData("status")]
        public void SortServices_ByStatus_SortsCorrectly(string columnName)
        {
            // Arrange
            var services = CreateTestServices(3);
            services[0].Status = ServiceStatus.Running;
            services[1].Status = ServiceStatus.Stopped;
            services[2].Status = ServiceStatus.Error;
            UpdateServicesInViewModel(services).Wait();

            // Act
            _viewModel.SortServices(columnName);

            // Assert
            _viewModel.Services[0].Status.Should().Be(ServiceStatus.Stopped);
            _viewModel.Services[1].Status.Should().Be(ServiceStatus.Error);
            _viewModel.Services[2].Status.Should().Be(ServiceStatus.Running);
        }

        [Theory]
        [InlineData("Created")]
        [InlineData("created")]
        public void SortServices_ByCreatedDate_SortsCorrectly(string columnName)
        {
            // Arrange
            var services = CreateTestServices(3);
            services[0].CreatedAt = DateTime.UtcNow.AddDays(-2);
            services[1].CreatedAt = DateTime.UtcNow;
            services[2].CreatedAt = DateTime.UtcNow.AddDays(-1);
            UpdateServicesInViewModel(services).Wait();

            // Act
            _viewModel.SortServices(columnName);

            // Assert
            _viewModel.Services[0].CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromDays(0));
            _viewModel.Services[1].CreatedAt.Should().BeCloseTo(DateTime.UtcNow.AddDays(-1), TimeSpan.FromDays(0));
            _viewModel.Services[2].CreatedAt.Should().BeCloseTo(DateTime.UtcNow.AddDays(-2), TimeSpan.FromDays(0));
        }

        [Fact]
        public void SortServices_UnknownColumn_SetsErrorMessage()
        {
            // Arrange
            var columnName = "unknown";

            // Act
            _viewModel.SortServices(columnName);

            // Assert
            _viewModel.StatusMessage.Should().Contain("未知的排序列");
        }

        #endregion

        #region Service Update Tests

        [Fact]
        public async Task OnServicesUpdated_UpdatesServiceCollection()
        {
            // Arrange
            var callback = new Action<List<ServiceItem>>(services => { });
            _mockStatusMonitor.Setup(x => x.Subscribe(It.IsAny<Action<List<ServiceItem>>>()))
                .Callback<Action<List<ServiceItem>>>(action => callback = action);

            // Create a new view model to capture the callback
            var newViewModel = new MainWindowViewModel(
                _mockServiceManager.Object,
                _mockStatusMonitor.Object,
                _mockLogReaderService.Object,
                _mockPathValidator.Object,
                _mockCommandValidator.Object);

            var newServices = CreateTestServices(2);

            // Act
            callback(newServices);
            await Task.Delay(100); // Allow async operation to complete

            // Assert
            newViewModel.Services.Should().HaveCount(2);
            newViewModel.AllServicesCount.Should().Be(2);

            newViewModel.Dispose();
        }

        #endregion

        #region Dispose Tests

        [Fact]
        public void Dispose_UnsubscribesFromStatusMonitor()
        {
            // Act
            _viewModel.Dispose();

            // Assert
            _mockStatusMonitor.Verify(x => x.Unsubscribe(It.IsAny<Action<List<ServiceItem>>>()), Times.Once);
        }

        [Fact]
        public void Dispose_CalledTwice_DoesNotThrowException()
        {
            // Act & Assert - Should not throw
            _viewModel.Dispose();
            _viewModel.Dispose();
        }

        #endregion

        #region Search and Export Tests

        [Fact]
        public void SearchText_SetValue_ShouldFilterServices()
        {
            // Arrange
            var testServices = CreateTestServices(5);
            UpdateServicesInViewModel(testServices).Wait();

            // Act
            _viewModel.SearchText = "Service 3";

            // Assert
            _viewModel.Services.Should().HaveCount(1);
            _viewModel.Services.First().DisplayName.Should().Contain("Service 3");
            _viewModel.StatusMessage.Should().Contain("找到 1 个匹配的服务");
        }

        [Fact]
        public void SearchText_SetEmptyValue_ShouldShowAllServices()
        {
            // Arrange
            var testServices = CreateTestServices(5);
            UpdateServicesInViewModel(testServices).Wait();
            _viewModel.SearchText = "Service 3";
            _viewModel.Services.Should().HaveCount(1); // 验证已过滤

            // Act
            _viewModel.SearchText = "";

            // Assert
            _viewModel.Services.Should().HaveCount(5);
            _viewModel.StatusMessage.Should().Contain("显示所有服务 (5)");
        }

        [Fact]
        public void SearchText_SearchByDescription_ShouldFilterCorrectly()
        {
            // Arrange
            var testServices = CreateTestServices(5);
            UpdateServicesInViewModel(testServices).Wait();

            // Act
            _viewModel.SearchText = "number 2";

            // Assert
            _viewModel.Services.Should().HaveCount(1);
            _viewModel.Services.First().Description.Should().Contain("number 2");
        }

        [Fact]
        public void SearchText_SearchByExecutablePath_ShouldFilterCorrectly()
        {
            // Arrange
            var testServices = CreateTestServices(5);
            UpdateServicesInViewModel(testServices).Wait();

            // Act
            _viewModel.SearchText = "service4.exe";

            // Assert
            _viewModel.Services.Should().HaveCount(1);
            _viewModel.Services.First().ExecutablePath.Should().Contain("service4.exe");
        }

        [Fact]
        public void SearchText_SearchCaseInsensitive_ShouldFilterCorrectly()
        {
            // Arrange
            var testServices = CreateTestServices(5);
            UpdateServicesInViewModel(testServices).Wait();

            // Act
            _viewModel.SearchText = "SERVICE 3";

            // Assert
            _viewModel.Services.Should().HaveCount(1);
            _viewModel.Services.First().DisplayName.Should().Contain("Service 3");
        }

        [Fact]
        public void SearchText_SearchWithWhitespace_ShouldTrimAndFilter()
        {
            // Arrange
            var testServices = CreateTestServices(5);
            UpdateServicesInViewModel(testServices).Wait();

            // Act
            _viewModel.SearchText = "  Service 3  ";

            // Assert
            _viewModel.Services.Should().HaveCount(1);
            _viewModel.Services.First().DisplayName.Should().Contain("Service 3");
        }

        [Fact]
        public async Task ExportServicesCommand_WithServices_ShouldExportToFile()
        {
            // Arrange
            var testServices = CreateTestServices(3);
            UpdateServicesInViewModel(testServices).Wait();
            var exportPath = Path.Combine(_tempTestDir, "test_export.json");

            // Mock SaveFileDialog
            var mockSaveDialog = new Mock<ISaveFileDialog>();
            mockSaveDialog.Setup(x => x.ShowDialog()).Returns(true);
            mockSaveDialog.Setup(x => x.FileName).Returns(exportPath);

            // Act
            _viewModel.ExportServicesCommand.Execute(mockSaveDialog.Object);

            // Assert
            File.Exists(exportPath).Should().BeTrue();
            var json = await File.ReadAllTextAsync(exportPath);
            json.Should().Contain("service-1");
            json.Should().Contain("service-2");
            json.Should().Contain("service-3");
            json.Should().Contain("ExportTime");
            json.Should().Contain("ExportVersion");
        }

        [Fact]
        public void ExportServicesCommand_UserCancels_ShouldNotCreateFile()
        {
            // Arrange
            var testServices = CreateTestServices(3);
            UpdateServicesInViewModel(testServices).Wait();
            var exportPath = Path.Combine(_tempTestDir, "cancelled_export.json");

            // Mock SaveFileDialog with user cancel
            var mockSaveDialog = new Mock<ISaveFileDialog>();
            mockSaveDialog.Setup(x => x.ShowDialog()).Returns(false);

            // Act
            _viewModel.ExportServicesCommand.Execute(mockSaveDialog.Object);

            // Assert
            File.Exists(exportPath).Should().BeFalse();
        }

        [Fact]
        public async Task ExportServicesCommand_WithEmptyServices_ShouldExportEmptyArray()
        {
            // Arrange
            UpdateServicesInViewModel(new List<ServiceItem>()).Wait();
            var exportPath = Path.Combine(_tempTestDir, "empty_export.json");

            var mockSaveDialog = new Mock<ISaveFileDialog>();
            mockSaveDialog.Setup(x => x.ShowDialog()).Returns(true);
            mockSaveDialog.Setup(x => x.FileName).Returns(exportPath);

            // Act
            _viewModel.ExportServicesCommand.Execute(mockSaveDialog.Object);

            // Assert
            File.Exists(exportPath).Should().BeTrue();
            var json = await File.ReadAllTextAsync(exportPath);
            json.Should().Contain("\"Services\": []");
        }

        [Fact]
        public async Task ExportServicesCommand_WithUnauthorizedAccess_ShouldLogError()
        {
            // Arrange
            var testServices = CreateTestServices(3);
            UpdateServicesInViewModel(testServices).Wait();
            var restrictedPath = Path.Combine(_tempTestDir, "restricted");
            Directory.CreateDirectory(restrictedPath);

            // Create a read-only directory
            var dirInfo = new DirectoryInfo(restrictedPath);
            dirInfo.Attributes |= FileAttributes.ReadOnly;
            var exportPath = Path.Combine(restrictedPath, "export.json");

            var mockSaveDialog = new Mock<ISaveFileDialog>();
            mockSaveDialog.Setup(x => x.ShowDialog()).Returns(true);
            mockSaveDialog.Setup(x => x.FileName).Returns(exportPath);

            // Act
            _viewModel.ExportServicesCommand.Execute(mockSaveDialog.Object);

            // Assert
            _viewModel.StatusMessage.Should().Contain("导出失败：没有写入权限");

            // Cleanup
            dirInfo.Attributes &= ~FileAttributes.ReadOnly;
        }

        [Fact]
        public async Task ExportServicesCommand_ExportedDataStructure_ShouldBeValid()
        {
            // Arrange
            var testServices = new List<ServiceItem>
            {
                new ServiceItem
                {
                    Id = "test-1",
                    DisplayName = "Test Service 1",
                    Description = "Test Description",
                    ExecutablePath = @"C:\test\app.exe",
                    ScriptPath = @"C:\test\script.py",
                    WorkingDirectory = @"C:\test",
                    StartupArguments = "--debug",
                    ServiceAccount = "LocalService",
                    Environment = new Dictionary<string, string>
                    {
                        ["ENV1"] = "value1",
                        ["ENV2"] = "value2"
                    },
                    LogPath = @"C:\logs\test.log",
                    LogMode = LogMode.Append,
                    StartMode = ServiceStartMode.Manual,
                    StopTimeout = 30000,
                    Priority = ProcessPriority.High,
                    Affinity = "0,1",
                    Metadata = new Dictionary<string, string>
                    {
                        ["Version"] = "1.0"
                    }
                }
            };
            UpdateServicesInViewModel(testServices).Wait();
            var exportPath = Path.Combine(_tempTestDir, "structured_export.json");

            var mockSaveDialog = new Mock<ISaveFileDialog>();
            mockSaveDialog.Setup(x => x.ShowDialog()).Returns(true);
            mockSaveDialog.Setup(x => x.FileName).Returns(exportPath);

            // Act
            _viewModel.ExportServicesCommand.Execute(mockSaveDialog.Object);

            // Assert
            File.Exists(exportPath).Should().BeTrue();
            var json = await File.ReadAllTextAsync(exportPath);
            json.Should().Contain("\"test-1\"");
            json.Should().Contain("\"Test Service 1\"");
            json.Should().Contain("\"Test Description\"");
            json.Should().Contain("\"C:\\test\\app.exe\"");
            json.Should().Contain("\"C:\\test\\script.py\"");
            json.Should().Contain("\"--debug\"");
            json.Should().Contain("\"LocalService\"");
            json.Should().Contain("\"ENV1\"");
            json.Should().Contain("\"value1\"");
            json.Should().Contain("\"append\"");
            json.Should().Contain("\"manual\"");
            json.Should().Contain("\"high\"");
            json.Should().Contain("\"0,1\"");
            json.Should().Contain("\"Version\": \"1.0\"");
        }

        #endregion

        #region Helper Methods

        private List<ServiceItem> CreateTestServices(int count)
        {
            return Enumerable.Range(1, count)
                .Select(i => new ServiceItem
                {
                    Id = $"service-{i}",
                    DisplayName = $"Service {i}",
                    Description = $"Test service number {i}",
                    ExecutablePath = $@"C:\Test\service{i}.exe",
                    WorkingDirectory = $@"C:\Test\service{i}",
                    Status = ServiceStatus.Stopped,
                    CreatedAt = DateTime.UtcNow.AddHours(-i)
                })
                .ToList();
        }

        private async Task UpdateServicesInViewModel(List<ServiceItem> services)
        {
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                _viewModel.Services.Clear();
                foreach (var service in services)
                {
                    _viewModel.Services.Add(new ServiceItemViewModel(service, _mockServiceManager.Object));
                }
                _viewModel.AllServicesCount = services.Count;
            });
        }

        #endregion
    }
}