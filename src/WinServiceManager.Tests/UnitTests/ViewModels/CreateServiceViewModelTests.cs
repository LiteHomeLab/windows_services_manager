using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using WinServiceManager.Models;
using WinServiceManager.Services;
using WinServiceManager.ViewModels;
using Xunit;

namespace WinServiceManager.Tests.UnitTests.ViewModels
{
    /// <summary>
    /// Unit tests for CreateServiceViewModel class
    /// Tests service creation functionality including validation and command execution
    /// </summary>
    public class CreateServiceViewModelTests : IDisposable
    {
        private readonly Mock<ServiceManagerService> _mockServiceManager;
        private readonly Mock<PathValidator> _mockPathValidator;
        private readonly Mock<CommandValidator> _mockCommandValidator;
        private readonly CreateServiceViewModel _viewModel;
        private readonly string _tempTestDir;

        public CreateServiceViewModelTests()
        {
            _mockServiceManager = new Mock<ServiceManagerService>(Mock.Of<WinSWWrapper>(), Mock.Of<IDataStorageService>());
            _mockPathValidator = new Mock<PathValidator>();
            _mockCommandValidator = new Mock<CommandValidator>();

            _tempTestDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(_tempTestDir);

            // Setup default validations
            _mockPathValidator.Setup(x => x.IsValidPath(It.IsAny<string>())).Returns(true);
            _mockCommandValidator.Setup(x => x.IsValidInput(It.IsAny<string>())).Returns(true);
            _mockCommandValidator.Setup(x => x.SanitizeInput(It.IsAny<string>())).Returns<string>(s => s);

            _viewModel = new CreateServiceViewModel(
                _mockServiceManager.Object,
                _mockPathValidator.Object,
                _mockCommandValidator.Object);
        }

        public void Dispose()
        {
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
                new CreateServiceViewModel(null!, _mockPathValidator.Object, _mockCommandValidator.Object));
        }

        [Fact]
        public void Constructor_NullPathValidator_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new CreateServiceViewModel(_mockServiceManager.Object, null!, _mockCommandValidator.Object));
        }

        [Fact]
        public void Constructor_NullCommandValidator_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new CreateServiceViewModel(_mockServiceManager.Object, _mockPathValidator.Object, null!));
        }

        [Fact]
        public void Constructor_ValidParameters_InitializesWithDefaults()
        {
            // Assert
            _viewModel.DisplayName.Should().BeEmpty();
            _viewModel.Description.Should().Be("Managed by WinServiceManager");
            _viewModel.ExecutablePath.Should().BeEmpty();
            _viewModel.Arguments.Should().BeEmpty();
            _viewModel.WorkingDirectory.Should().BeEmpty();
            _viewModel.AutoStart.Should().BeTrue();
            _viewModel.AutoRestart.Should().BeTrue();
            _viewModel.IsBusy.Should().BeFalse();
            _viewModel.ErrorMessage.Should().BeEmpty();
            _viewModel.CanCreate.Should().BeFalse();
            _viewModel.IsScriptFileEnabled.Should().BeFalse();
        }

        #endregion

        #region Property Tests

        [Theory]
        [InlineData("Test Service", true)]
        [InlineData("", false)]
        [InlineData("Te", false)] // Too short
        [InlineData(new string('a', 101), false)] // Too long
        [InlineData("   ", false)]
        public void DisplayName_Validation_UpdatesCanCreate(string displayName, bool expectedCanCreate)
        {
            // Setup - Create a valid executable file
            var exePath = Path.Combine(_tempTestDir, "test.exe");
            File.WriteAllText(exePath, "test");
            _viewModel.ExecutablePath = exePath;
            _viewModel.WorkingDirectory = _tempTestDir;

            // Act
            _viewModel.DisplayName = displayName;

            // Assert
            _viewModel.CanCreate.Should().Be(expectedCanCreate);
        }

        [Theory]
        [InlineData(@"C:\Program Files\Python\python.exe", true)]
        [InlineData(@"C:\Program Files\nodejs\node.exe", true)]
        [InlineData(@"C:\Program Files\Java\java.exe", true)]
        [InlineData(@"C:\Program Files\ruby\ruby.exe", true)]
        [InlineData(@"C:\Windows\System32\cmd.exe", true)]
        [InlineData(@"C:\Program Files\app.exe", false)]
        [InlineData("", false)]
        [InlineData(null, false)]
        public void ExecutablePath_SetsIsScriptFileEnabled(string executablePath, bool expectedScriptEnabled)
        {
            // Act
            _viewModel.ExecutablePath = executablePath;

            // Assert
            _viewModel.IsScriptFileEnabled.Should().Be(expectedScriptEnabled);
        }

        [Fact]
        public void ExecutablePath_ValidPath_SetsWorkingDirectoryAutomatically()
        {
            // Arrange
            var exePath = Path.Combine(_tempTestDir, "app.exe");
            File.WriteAllText(exePath, "test");

            // Act
            _viewModel.ExecutablePath = exePath;

            // Assert
            _viewModel.WorkingDirectory.Should().Be(_tempTestDir);
        }

        [Fact]
        public void ExecutablePath_WithExistingWorkingDirectory_DoesNotOverride()
        {
            // Arrange
            var existingWorkDir = Path.Combine(_tempTestDir, "work");
            Directory.CreateDirectory(existingWorkDir);
            var exePath = Path.Combine(_tempTestDir, "app.exe");
            File.WriteAllText(exePath, "test");
            _viewModel.WorkingDirectory = existingWorkDir;

            // Act
            _viewModel.ExecutablePath = exePath;

            // Assert
            _viewModel.WorkingDirectory.Should().Be(existingWorkDir);
        }

        [Fact]
        public void IsBusy_True_SetsCanCreateToFalse()
        {
            // Arrange - Set up valid data first
            _viewModel.DisplayName = "Test Service";
            _viewModel.ExecutablePath = Path.Combine(_tempTestDir, "test.exe");
            File.WriteAllText(_viewModel.ExecutablePath, "test");
            _viewModel.WorkingDirectory = _tempTestDir;

            // Act
            _viewModel.IsBusy = true;

            // Assert
            _viewModel.CanCreate.Should().BeFalse();
        }

        #endregion

        #region PreviewConfigCommand Tests

        [Fact]
        public void PreviewConfigCommand_ValidData_GeneratesPreview()
        {
            // Arrange
            SetupValidServiceData();

            // Act
            _viewModel.PreviewConfigCommand.Execute(null);

            // Assert
            _viewModel.ShowPreview.Should().BeTrue();
            _viewModel.ConfigPreview.Should().NotBeEmpty();
            _viewModel.ErrorMessage.Should().BeEmpty();
        }

        [Fact]
        public void PreviewConfigCommand_InvalidData_SetsErrorMessage()
        {
            // Arrange - No valid data
            _viewModel.DisplayName = "";

            // Act
            _viewModel.PreviewConfigCommand.Execute(null);

            // Assert
            _viewModel.ShowPreview.Should().BeFalse();
            _viewModel.ErrorMessage.Should().Contain("请先填写所有必填字段");
        }

        [Fact]
        public void PreviewConfigCommand_ExceptionThrown_SetsErrorAndHidesPreview()
        {
            // Arrange
            SetupValidServiceData();
            // Make GenerateWinSWConfig throw by setting up invalid data that causes exception
            _viewModel.Arguments = new string('a', 10000); // Very long arguments

            // Act
            _viewModel.PreviewConfigCommand.Execute(null);

            // Assert
            _viewModel.ShowPreview.Should().BeFalse();
            _viewModel.ErrorMessage.Should().StartWith("生成配置预览失败:");
        }

        #endregion

        #region CreateCommand Tests

        [Fact]
        public async Task CreateCommand_ValidData_CreatesServiceSuccessfully()
        {
            // Arrange
            SetupValidServiceData();
            var expectedServiceId = "service-123";
            _mockServiceManager.Setup(x => x.CreateServiceAsync(It.IsAny<ServiceCreateRequest>()))
                .ReturnsAsync(new ServiceOperationResult<string>
                {
                    Success = true,
                    Data = expectedServiceId
                });

            var closeRequested = false;
            _viewModel.RequestClose += () => closeRequested = true;

            // Act
            await _viewModel.CreateCommand.ExecuteAsync(null);

            // Assert
            _viewModel.IsBusy.Should().BeFalse();
            _viewModel.ErrorMessage.Should().BeEmpty();
            closeRequested.Should().BeTrue();
            _mockServiceManager.Verify(x => x.CreateServiceAsync(It.IsAny<ServiceCreateRequest>()), Times.Once);
            _mockCommandValidator.Verify(x => x.SanitizeInput(It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task CreateCommand_InvalidData_SetsErrorMessage()
        {
            // Arrange - No valid data
            _viewModel.DisplayName = "";

            // Act
            await _viewModel.CreateCommand.ExecuteAsync(null);

            // Assert
            _viewModel.IsBusy.Should().BeFalse();
            _viewModel.ErrorMessage.Should().Contain("请检查输入");
            _mockServiceManager.Verify(x => x.CreateServiceAsync(It.IsAny<ServiceCreateRequest>()), Times.Never);
        }

        [Fact]
        public async Task CreateCommand_ServiceManagerFails_SetsErrorMessage()
        {
            // Arrange
            SetupValidServiceData();
            var errorMessage = "Service creation failed";
            _mockServiceManager.Setup(x => x.CreateServiceAsync(It.IsAny<ServiceCreateRequest>()))
                .ReturnsAsync(new ServiceOperationResult<string>
                {
                    Success = false,
                    ErrorMessage = errorMessage
                });

            // Act
            await _viewModel.CreateCommand.ExecuteAsync(null);

            // Assert
            _viewModel.IsBusy.Should().BeFalse();
            _viewModel.ErrorMessage.Should().Contain(errorMessage);
        }

        [Fact]
        public async Task CreateCommand_ExceptionThrown_SetsErrorMessage()
        {
            // Arrange
            SetupValidServiceData();
            _mockServiceManager.Setup(x => x.CreateServiceAsync(It.IsAny<ServiceCreateRequest>()))
                .ThrowsAsync(new InvalidOperationException("Test exception"));

            // Act
            await _viewModel.CreateCommand.ExecuteAsync(null);

            // Assert
            _viewModel.IsBusy.Should().BeFalse();
            _viewModel.ErrorMessage.Should().Contain("创建服务时发生错误");
        }

        [Fact]
        public void CreateCommand_CanExecute_RespectsCanCreateProperty()
        {
            // Arrange
            SetupValidServiceData();

            // Act & Assert
            _viewModel.CreateCommand.CanExecute(null).Should().BeTrue();

            // Arrange
            _viewModel.IsBusy = true;

            // Act & Assert
            _viewModel.CreateCommand.CanExecute(null).Should().BeFalse();
        }

        #endregion

        #region CancelCommand Tests

        [Fact]
        public void CancelCommand_Executed_RaisesRequestClose()
        {
            // Arrange
            var closeRequested = false;
            _viewModel.RequestClose += () => closeRequested = true;

            // Act
            _viewModel.CancelCommand.Execute(null);

            // Assert
            closeRequested.Should().BeTrue();
        }

        #endregion

        #region Validation Tests

        [Fact]
        public void Validation_EmptyDisplayName_SetsError()
        {
            // Arrange
            _viewModel.DisplayName = "";
            _viewModel.ExecutablePath = Path.Combine(_tempTestDir, "test.exe");
            File.WriteAllText(_viewModel.ExecutablePath, "test");
            _viewModel.WorkingDirectory = _tempTestDir;

            // Act
            var canCreate = _viewModel.CanCreate;

            // Assert
            canCreate.Should().BeFalse();
            _viewModel.ErrorMessage.Should().Contain("服务名称不能为空");
        }

        [Fact]
        public void Validation_DisplayNameTooShort_SetsError()
        {
            // Arrange
            _viewModel.DisplayName = "ab"; // Less than 3 characters
            _viewModel.ExecutablePath = Path.Combine(_tempTestDir, "test.exe");
            File.WriteAllText(_viewModel.ExecutablePath, "test");
            _viewModel.WorkingDirectory = _tempTestDir;

            // Act
            var canCreate = _viewModel.CanCreate;

            // Assert
            canCreate.Should().BeFalse();
            _viewModel.ErrorMessage.Should().Contain("服务名称长度必须在3-100个字符之间");
        }

        [Fact]
        public void Validation_InvalidExecutablePath_SetsError()
        {
            // Arrange
            _viewModel.DisplayName = "Test Service";
            _viewModel.ExecutablePath = "nonexistent.exe";
            _viewModel.WorkingDirectory = _tempTestDir;

            // Act
            var canCreate = _viewModel.CanCreate;

            // Assert
            canCreate.Should().BeFalse();
            _viewModel.ErrorMessage.Should().Contain("指定的可执行文件不存在");
        }

        [Fact]
        public void Validation_InvalidScriptPath_SetsError()
        {
            // Arrange
            _viewModel.DisplayName = "Test Service";
            _viewModel.ExecutablePath = Path.Combine(_tempTestDir, "python.exe");
            File.WriteAllText(_viewModel.ExecutablePath, "test");
            _viewModel.ScriptPath = "nonexistent.py";
            _viewModel.WorkingDirectory = _tempTestDir;

            // Act
            var canCreate = _viewModel.CanCreate;

            // Assert
            canCreate.Should().BeFalse();
            _viewModel.ErrorMessage.Should().Contain("指定的脚本文件不存在");
        }

        [Fact]
        public void Validation_InvalidWorkingDirectory_SetsError()
        {
            // Arrange
            _viewModel.DisplayName = "Test Service";
            _viewModel.ExecutablePath = Path.Combine(_tempTestDir, "test.exe");
            File.WriteAllText(_viewModel.ExecutablePath, "test");
            _viewModel.WorkingDirectory = "nonexistent";

            // Act
            var canCreate = _viewModel.CanCreate;

            // Assert
            canCreate.Should().BeFalse();
            _viewModel.ErrorMessage.Should().Contain("指定的工作目录不存在");
        }

        [Fact]
        public void Validation_InvalidArguments_SetsError()
        {
            // Arrange
            _viewModel.DisplayName = "Test Service";
            _viewModel.ExecutablePath = Path.Combine(_tempTestDir, "test.exe");
            File.WriteAllText(_viewModel.ExecutablePath, "test");
            _viewModel.WorkingDirectory = _tempTestDir;
            _viewModel.Arguments = "invalid;command";
            _mockCommandValidator.Setup(x => x.IsValidInput("invalid;command")).Returns(false);

            // Act
            var canCreate = _viewModel.CanCreate;

            // Assert
            canCreate.Should().BeFalse();
            _viewModel.ErrorMessage.Should().Contain("启动参数包含非法字符");
        }

        [Fact]
        public void Validation_PathValidatorRejectsPath_SetsError()
        {
            // Arrange
            _viewModel.DisplayName = "Test Service";
            _viewModel.ExecutablePath = Path.Combine(_tempTestDir, "test.exe");
            File.WriteAllText(_viewModel.ExecutablePath, "test");
            _viewModel.WorkingDirectory = _tempTestDir;
            _mockPathValidator.Setup(x => x.IsValidPath(_viewModel.ExecutablePath)).Returns(false);

            // Act
            var canCreate = _viewModel.CanCreate;

            // Assert
            canCreate.Should().BeFalse();
            _viewModel.ErrorMessage.Should().Contain("可执行文件路径包含非法字符");
        }

        [Fact]
        public void Validation_AllValid_ClearsErrorAndAllowsCreate()
        {
            // Arrange
            SetupValidServiceData();

            // Act
            var canCreate = _viewModel.CanCreate;

            // Assert
            canCreate.Should().BeTrue();
            _viewModel.ErrorMessage.Should().BeEmpty();
        }

        #endregion

        #region Helper Methods

        private void SetupValidServiceData()
        {
            var exePath = Path.Combine(_tempTestDir, "test.exe");
            File.WriteAllText(exePath, "test");

            _viewModel.DisplayName = "Test Service";
            _viewModel.Description = "Test Description";
            _viewModel.ExecutablePath = exePath;
            _viewModel.Arguments = "--test";
            _viewModel.WorkingDirectory = _tempTestDir;
            _viewModel.AutoStart = true;
        }

        #endregion
    }
}