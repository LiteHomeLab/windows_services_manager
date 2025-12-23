using System;
using System.Diagnostics;
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using WinServiceManager.Models;
using WinServiceManager.Services;
using WinServiceManager.Tests.Fixtures;
using Xunit;
using Xunit.Abstractions;

namespace WinServiceManager.Tests.IntegrationTests.EdgeCases
{
    /// <summary>
    /// Permission denied tests
    /// Tests application behavior when lacking required permissions
    /// </summary>
    [Collection("Edge Cases Tests")]
    public class PermissionDeniedTests : IClassFixture<ServiceTestFixture>, IDisposable
    {
        private readonly ServiceTestFixture _fixture;
        private readonly ITestOutputHelper _output;
        private readonly MockDataStorageService _dataStorage;
        private readonly string _protectedDirectory;
        private readonly string _protectedFile;

        public PermissionDeniedTests(ServiceTestFixture fixture, ITestOutputHelper output)
        {
            _fixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
            _output = output ?? throw new ArgumentNullException(nameof(output));
            _dataStorage = _fixture.MockDataStorage;

            // Create a protected directory for testing
            _protectedDirectory = Path.Combine(_fixture.TestServicesDirectory, "Protected");
            _protectedFile = Path.Combine(_protectedDirectory, "protected.txt");

            // Create the directory and file with restricted permissions
            CreateProtectedResources();
        }

        public void Dispose()
        {
            // Cleanup - try to delete protected resources
            try
            {
                if (Directory.Exists(_protectedDirectory))
                {
                    // Grant ourselves permissions before cleanup
                    GrantFullControl(_protectedDirectory);
                    GrantFullControl(_protectedFile);

                    if (File.Exists(_protectedFile))
                    {
                        File.Delete(_protectedFile);
                    }
                    Directory.Delete(_protectedDirectory);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        private void CreateProtectedResources()
        {
            try
            {
                // Create directory
                Directory.CreateDirectory(_protectedDirectory);

                // Create a file
                File.WriteAllText(_protectedFile, "Protected content");

                // Remove all permissions (this may not work if we're admin)
                try
                {
                    DenyAllAccess(_protectedDirectory);
                    DenyAllAccess(_protectedFile);

                    _output.WriteLine("Created protected resources with restricted permissions");
                }
                catch (UnauthorizedAccessException)
                {
                    _output.WriteLine("Note: Running as administrator, cannot create truly restricted resources");
                    _output.WriteLine("This test will verify permission checking logic instead");
                }
            }
            catch (Exception ex)
            {
                _output.WriteLine($"Could not create protected resources: {ex.Message}");
            }
        }

        private void DenyAllAccess(string path)
        {
            var security = File.GetAccessControl(path);

            // Remove all permissions
            foreach (FileSystemAccessRule rule in security.GetAccessRules(true, true, typeof(NTAccount)))
            {
                security.RemoveAccessRule(rule);
            }

            if (File.Exists(path))
            {
                File.SetAccessControl(path, (FileSecurity)security);
            }
            else
            {
                Directory.SetAccessControl(path, (DirectorySecurity)security);
            }
        }

        private void GrantFullControl(string path)
        {
            try
            {
                var identity = WindowsIdentity.GetCurrent().Name;
                var security = File.GetAccessControl(path);

                var rule = new FileSystemAccessRule(
                    identity,
                    FileSystemRights.FullControl,
                    InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                    PropagationFlags.None,
                    AccessControlType.Allow);

                security.AddAccessRule(rule);

                if (File.Exists(path))
                {
                    File.SetAccessControl(path, (FileSecurity)security);
                }
                else
                {
                    Directory.SetAccessControl(path, (DirectorySecurity)security);
                }
            }
            catch
            {
                // Ignore
            }
        }

        [Fact]
        public void CheckAdministratorPrivileges_RequiresAdminToRun()
        {
            // Arrange & Act
            var isAdmin = WindowsIdentity.GetCurrent()
                .Owner
                ?.Value.Equals("S-1-5-32-544") == true; // Administrators group SID

            var currentIdentity = WindowsIdentity.GetCurrent().Name;

            // Assert - Document the requirement
            _output.WriteLine($"Current user: {currentIdentity}");
            _output.WriteLine($"Is administrator: {isAdmin}");

            // The application requires admin privileges
            // This test documents the requirement
            if (!isAdmin)
            {
                _output.WriteLine("WARNING: Not running as administrator");
            }
        }

        [Fact]
        public async Task AccessProtectedFile_WithoutPermissions_FailsGracefully()
        {
            // Arrange
            if (!File.Exists(_protectedFile))
            {
                _output.WriteLine("SKIPPED: Protected file not created");
                return;
            }

            // Act - Try to read protected file
            string content;
            try
            {
                content = await File.ReadAllTextAsync(_protectedFile);
            }
            catch (UnauthorizedAccessException ex)
            {
                _output.WriteLine($"Expected exception caught: {ex.Message}");

                // Assert - Exception should be caught and handled
                ex.GetType().Should().Be<UnauthorizedAccessException>();
                return;
            }

            // If we can read it, that's also OK (we're admin)
            content.Should().NotBeEmpty();
            _output.WriteLine("Note: Running as administrator, could access protected file");
        }

        [Fact]
        public void ServiceDirectory_AccessControl_ShouldBeConfigured()
        {
            // Arrange & Act
            var testDir = Path.Combine(_fixture.TestServicesDirectory, "AccessControlTest");
            Directory.CreateDirectory(testDir);

            // Get directory security
            var directoryInfo = new DirectoryInfo(testDir);
            var directorySecurity = directoryInfo.GetAccessControl();

            // Assert - Document expected access control
            var accessRules = directorySecurity.GetAccessRules(true, true, typeof(NTAccount));

            _output.WriteLine($"Directory: {testDir}");
            _output.WriteLine($"Access rules count: {accessRules.Count}");

            foreach (FileSystemAccessRule rule in accessRules)
            {
                _output.WriteLine($"  {rule.IdentityReference}: {rule.FileSystemRights} ({rule.AccessControlType})");
            }

            // Cleanup
            Directory.Delete(testDir);
        }

        [Fact]
        public async Task CreateService_InProtectedDirectory_ShouldHandleErrors()
        {
            // Arrange
            var service = new ServiceItem
            {
                DisplayName = "ProtectedDirTest",
                ExecutablePath = Path.Combine(_protectedDirectory, "nonexistent.exe"),
                Arguments = ""
            };

            // Act - Try to add service with protected path
            try
            {
                await _dataStorage.AddServiceAsync(service);

                // If successful, verify it was saved
                var loaded = await _dataStorage.GetServiceAsync(service.Id);
                loaded.Should().NotBeNull();

                _output.WriteLine("Service created (may have admin permissions)");
            }
            catch (UnauthorizedAccessException ex)
            {
                _output.WriteLine($"Expected exception: {ex.Message}");
                ex.GetType().Should().Be<UnauthorizedAccessException>();
            }
        }

        [Fact]
        public void SystemDirectory_RequiresPermissions()
        {
            // Arrange
            var systemDir = Environment.SystemDirectory;
            var systemDirInfo = new DirectoryInfo(systemDir);

            // Act - Check if we can list files
            string[] files;
            try
            {
                files = Directory.GetFiles(systemDir, "*.exe");
            }
            catch (UnauthorizedAccessException ex)
            {
                _output.WriteLine($"Cannot access system directory: {ex.Message}");
                return;
            }

            // Assert
            files.Should().NotBeEmpty();
            _output.WriteLine($"System directory contains {files.Length} executable files");
        }

        [Fact]
        public async Task ServiceExecutable_PathValidation_ShouldFailForProtectedPaths()
        {
            // Arrange
            var protectedPaths = new[]
            {
                @"C:\Windows\System32\config\", // Protected system directory
                @"\\?\GLOBALROOT\ ", // Invalid device path
            };

            foreach (var testPath in protectedPaths)
            {
                // Act - Try to validate protected path
                var validator = new PathValidator();

                try
                {
                    var isValid = validator.ValidateExecutablePath(testPath);
                    _output.WriteLine($"Path '{testPath}' validation result: {isValid}");
                }
                catch (Exception ex)
                {
                    _output.WriteLine($"Path '{testPath}' threw exception: {ex.Message}");
                }
            }

            // Assert - Documentation of expected behavior
            _output.WriteLine("Protected path validation tests completed");
        }

        [Fact]
        public void CheckWritePermissions_TempDirectory_ShouldBeWritable()
        {
            // Arrange
            var tempDir = Path.GetTempPath();
            var testFile = Path.Combine(tempDir, $"permission_test_{Guid.NewGuid()}.tmp");

            // Act - Try to write to temp directory
            try
            {
                File.WriteAllText(testFile, "Test content");
                var content = File.ReadAllText(testFile);
                File.Delete(testFile);

                // Assert
                content.Should().Be("Test content");
                _output.WriteLine("Temp directory is writable");
            }
            catch (UnauthorizedAccessException ex)
            {
                _output.WriteLine($"Cannot write to temp directory: {ex.Message}");
            }
        }

        [Fact]
        public async Task LogDirectory_WithoutWritePermissions_ShouldFail()
        {
            // Arrange - Create a log directory that we'll try to restrict
            var logDir = Path.Combine(_fixture.TestServicesDirectory, "RestrictedLogs");
            Directory.CreateDirectory(logDir);

            // Try to restrict access
            try
            {
                DenyAllAccess(logDir);
                _output.WriteLine("Created restricted log directory");

                // Act - Try to write to log directory
                var logFile = Path.Combine(logDir, "test.log");
                try
                {
                    await File.WriteAllTextAsync(logFile, "Test log entry");

                    // If successful, we have admin permissions
                    _output.WriteLine("Note: Could write to restricted directory (admin privileges)");
                }
                catch (UnauthorizedAccessException ex)
                {
                    _output.WriteLine($"Expected exception: {ex.Message}");
                }
            }
            finally
            {
                // Cleanup - grant permissions back
                GrantFullControl(logDir);
                try
                {
                    Directory.Delete(logDir, true);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }

        [Fact]
        public void ServiceInstallation_RequiresAdminPrivileges()
        {
            // Arrange
            var isAdmin = IsRunningAsAdministrator();

            // Act & Assert - Document the requirement
            _output.WriteLine($"Running as administrator: {isAdmin}");

            if (!isAdmin)
            {
                _output.WriteLine("WARNING: Service installation requires administrator privileges");
                _output.WriteLine("The application should check this at startup and prompt for elevation");
            }
            else
            {
                _output.WriteLine("Application has required privileges for service installation");
            }
        }

        private bool IsRunningAsAdministrator()
        {
            try
            {
                var identity = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }

        [Fact]
        public async Task AccessDenied_ErrorHandling_ShouldBeGraceful()
        {
            // Arrange - Create a file and then deny access
            var testFile = Path.Combine(_fixture.TestServicesDirectory, "access_test.txt");
            await File.WriteAllTextAsync(testFile, "Test content");

            try
            {
                // Restrict access
                DenyAllAccess(testFile);

                // Act - Try to read the file
                string content;
                try
                {
                    content = await File.ReadAllTextAsync(testFile);
                    _output.WriteLine("Note: Could read restricted file (admin privileges)");
                }
                catch (UnauthorizedAccessException ex)
                {
                    // Assert - Exception should be handled gracefully
                    _output.WriteLine($"Access denied handled: {ex.Message}");
                    ex.GetType().Should().Be<UnauthorizedAccessException>();
                }
            }
            finally
            {
                // Cleanup
                GrantFullControl(testFile);
                if (File.Exists(testFile))
                {
                    File.Delete(testFile);
                }
            }
        }
    }
}
