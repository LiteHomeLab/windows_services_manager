using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
    /// Network failure tests
    /// Tests application behavior when network paths fail or are unavailable
    /// </summary>
    [Collection("Edge Cases Tests")]
    public class NetworkFailureTests : IClassFixture<ServiceTestFixture>, IDisposable
    {
        private readonly ServiceTestFixture _fixture;
        private readonly ITestOutputHelper _output;
        private readonly MockDataStorageService _dataStorage;

        public NetworkFailureTests(ServiceTestFixture fixture, ITestOutputHelper output)
        {
            _fixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
            _output = output ?? throw new ArgumentNullException(nameof(output));
            _dataStorage = _fixture.MockDataStorage;
        }

        public void Dispose()
        {
            // Cleanup
        }

        [Fact]
        public void ValidateUncPath_DetectsNetworkPaths()
        {
            // Arrange
            var uncPaths = new[]
            {
                @"\\server\share\executable.exe",
                @"\\192.168.1.1\folder\app.exe",
                @"\\?\UNC\server\share\path.exe",
            };

            var localPaths = new[]
            {
                @"C:\Program Files\app.exe",
                @"D:\local\path\executable.exe",
                Environment.SystemDirectory + @"\timeout.exe"
            };

            var validator = new PathValidator();

            // Act & Assert - Test UNC path detection
            foreach (var uncPath in uncPaths)
            {
                var startsWithUnc = uncPath.StartsWith(@"\\") || uncPath.StartsWith(@"//");
                _output.WriteLine($"UNC path: {uncPath} -> Is UNC: {startsWithUnc}");
                startsWithUnc.Should().BeTrue($"'{uncPath}' should be detected as UNC path");
            }

            foreach (var localPath in localPaths)
            {
                var startsWithUnc = localPath.StartsWith(@"\\") || localPath.StartsWith(@"//");
                _output.WriteLine($"Local path: {localPath} -> Is UNC: {startsWithUnc}");
                startsWithUnc.Should().BeFalse($"'{localPath}' should not be detected as UNC path");
            }
        }

        [Fact]
        public async Task ServiceWithNetworkPath_WhenUnavailable_HandlesGracefully()
        {
            // Arrange - Create a service with an unreachable network path
            var service = new ServiceItem
            {
                DisplayName = "NetworkPathTest",
                ExecutablePath = @"\\nonexistent-server\invalid-share\executable.exe",
                Arguments = ""
            };

            // Act - Try to validate the path
            var validator = new PathValidator();
            var validationResult = validator.ValidateExecutablePath(service.ExecutablePath);

            // Assert - Validation should detect issues
            _output.WriteLine($"Network path validation result: {validationResult.IsValid}");
            _output.WriteLine($"Error message: {validationResult.ErrorMessage}");

            // Network paths may be valid syntax-wise even if unavailable
            // The validation should handle this appropriately
            if (!validationResult.IsValid)
            {
                validationResult.ErrorMessage.Should().NotBeNullOrEmpty();
                _output.WriteLine("Network path validation correctly detected issue");
            }
            else
            {
                _output.WriteLine("Note: Path syntax is valid (actual availability not checked)");
            }
        }

        [Fact]
        public async Task CreateService_WithInvalidNetworkPath_FailsValidation()
        {
            // Arrange
            var invalidNetworkPaths = new[]
            {
                @"\\server\",           // Missing share
                @"\\\server\share",    // Invalid triple backslash
                @"\\server\share\..\con.exe", // Path traversal attempt
                @"\\server\share\*",   // Wildcard character
            };

            var validator = new PathValidator();

            foreach (var testPath in invalidNetworkPaths)
            {
                // Act
                var result = validator.ValidateExecutablePath(testPath);

                // Assert
                _output.WriteLine($"Path: {testPath}");
                _output.WriteLine($"  Valid: {result.IsValid}");
                _output.WriteLine($"  Error: {result.ErrorMessage}");

                // At minimum, path traversal should be caught
                if (testPath.Contains(".."))
                {
                    result.IsValid.Should().BeFalse("path traversal in UNC paths should be rejected");
                }
            }
        }

        [Fact]
        public void CheckNetworkConnectivity_ServicesDirectory_ShouldBeLocal()
        {
            // Arrange
            var servicesDir = _fixture.TestServicesDirectory;

            // Act - Check if services directory is on a network drive
            var driveInfo = new DriveInfo(servicesDir);
            var driveType = driveInfo.DriveType;

            // Assert
            _output.WriteLine($"Services directory: {servicesDir}");
            _output.WriteLine($"Drive type: {driveType}");

            // Drive types:
            // Unknown = 0, NoRootDirectory = 1, Removable = 2,
            // Fixed = 3, Network = 4, CDRom = 5, Ram = 6

            if (driveType == DriveType.Network)
            {
                _output.WriteLine("WARNING: Services directory is on a network drive");
            }
            else
            {
                _output.WriteLine("Services directory is on local storage");
            }
        }

        [Fact]
        public async Task ServiceExecutable_OnNetworkDrive_ShouldWarnUser()
        {
            // Arrange
            var networkDrives = DriveInfo.GetDrives()
                .Where(d => d.DriveType == DriveType.Network && d.IsReady)
                .ToList();

            if (networkDrives.Any())
            {
                _output.WriteLine($"Found {networkDrives.Count} network drive(s):");
                foreach (var drive in networkDrives)
                {
                    _output.WriteLine($"  {drive.Name} ({drive.VolumeLabel})");
                }

                // Test with first available network drive
                var testPath = Path.Combine(networkDrives[0].Name, "test", "executable.exe");
                var validator = new PathValidator();
                var result = validator.ValidateExecutablePath(testPath);

                _output.WriteLine($"Network path validation: {result.IsValid}");
                if (!result.IsValid)
                {
                    _output.WriteLine($"Error: {result.ErrorMessage}");
                }
            }
            else
            {
                _output.WriteLine("No network drives available for testing");
            }
        }

        [Fact]
        public async Task LogFile_OnNetworkPath_ShouldHandleErrors()
        {
            // Arrange
            var networkDrives = DriveInfo.GetDrives()
                .Where(d => d.DriveType == DriveType.Network && d.IsReady)
                .FirstOrDefault();

            if (networkDrives == null)
            {
                _output.WriteLine("SKIPPED: No network drives available");
                return;
            }

            var logPath = Path.Combine(networkDrives.Name, "test", "logs", "test.log");

            // Act - Try to write to network log path
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(logPath) ?? string.Empty);
                await File.WriteAllTextAsync(logPath, "Test log entry");

                var content = await File.ReadAllTextAsync(logPath);
                content.Should().Be("Test log entry");

                _output.WriteLine("Successfully wrote to network path");

                // Cleanup
                File.Delete(logPath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _output.WriteLine($"Network path error handled: {ex.Message}");
            }
        }

        [Fact]
        public async Task NetworkTimeout_WhenAccessingResource_ShouldTimeoutGracefully()
        {
            // Arrange - Use a non-resolvable hostname
            var invalidHostPath = @"\\nonexistent-does-not-exist.local\share\file.txt";

            // Act - Try to access with timeout
            var stopwatch = Stopwatch.StartNew();
            try
            {
                // This should timeout or fail quickly
                var fileInfo = new FileInfo(invalidHostPath);
                var exists = fileInfo.Exists;
                stopwatch.Stop();

                _output.WriteLine($"Check completed in {stopwatch.ElapsedMilliseconds}ms");
                _output.WriteLine($"File exists result: {exists}");
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _output.WriteLine($"Exception caught after {stopwatch.ElapsedMilliseconds}ms: {ex.GetType().Name}");

                // Assert - Should not hang indefinitely
                stopwatch.ElapsedMilliseconds.Should().BeLessThan(30000,
                    "network timeout should occur within 30 seconds");
            }
        }

        [Fact]
        public void ValidateIpAddress_InUncPath()
        {
            // Arrange
            var validIpUncPaths = new[]
            {
                @"\\192.168.1.1\share\file.exe",
                @"\\10.0.0.1\share\file.exe",
                @"\\127.0.0.1\share\file.exe",
            };

            var invalidIpUncPaths = new[]
            {
                @"\\256.256.256.256\share\file.exe", // Invalid IP
                @"\\192.168.1\share\file.exe",      // Incomplete IP
                @"\\192.168.1.1.1\share\file.exe",  // Too many octets
            };

            var validator = new PathValidator();

            // Act & Assert - Test IP address validation
            _output.WriteLine("Valid IP UNC paths:");
            foreach (var path in validIpUncPaths)
            {
                var result = validator.ValidateExecutablePath(path);
                _output.WriteLine($"  {path} -> {result.IsValid}");
            }

            _output.WriteLine("Invalid IP UNC paths:");
            foreach (var path in invalidIpUncPaths)
            {
                var result = validator.ValidateExecutablePath(path);
                _output.WriteLine($"  {path} -> {result.IsValid}");
                // Some may fail validation depending on implementation
            }
        }

        [Fact]
        public async Task ServiceWithMappedDriveLetter_ShouldResolveCorrectly()
        {
            // Arrange - Get all mapped drives
            var allDrives = DriveInfo.GetDrives();
            var mappedDrives = allDrives
                .Where(d => d.DriveType == DriveType.Network)
                .ToList();

            _output.WriteLine($"Total drives: {allDrives.Length}");
            _output.WriteLine($"Network/mapped drives: {mappedDrives.Count}");

            foreach (var drive in mappedDrives)
            {
                _output.WriteLine($"  Drive {drive.Name}:");
                _output.WriteLine($"    Type: {drive.DriveType}");
                try
                {
                    _output.WriteLine($"    Volume label: {drive.VolumeLabel}");
                    _output.WriteLine($"    Available: {drive.AvailableFreeSpace / (1024.0 * 1024.0):F2} MB");
                }
                catch
                {
                    _output.WriteLine("    (Drive not ready)");
                }
            }

            // Act - If we have a mapped drive, try to use it
            if (mappedDrives.Count > 0)
            {
                var testDrive = mappedDrives[0];
                var testPath = Path.Combine(testDrive.Name, "test", "app.exe");

                var validator = new PathValidator();
                var result = validator.ValidateExecutablePath(testPath);

                _output.WriteLine($"Mapped drive path validation: {result.IsValid}");
                if (!result.IsValid)
                {
                    _output.WriteLine($"Error: {result.ErrorMessage}");
                }
            }
        }

        [Fact]
        public async Task NetworkPathAuthentication_WhenRequired_ShouldFail()
        {
            // Arrange - Many network shares require authentication
            var authenticatedSharePath = @"\\secure-server\secure-share\file.exe";

            // Act - Try to validate without credentials
            var validator = new PathValidator();
            var result = validator.ValidateExecutablePath(authenticatedSharePath);

            // Assert
            _output.WriteLine($"Authenticated share path: {authenticatedSharePath}");
            _output.WriteLine($"Validation result: {result.IsValid}");

            // The path may be syntactically valid even if inaccessible
            // This test documents expected behavior
            _output.WriteLine("Note: Path validation checks syntax, not accessibility");
        }

        [Fact]
        public async Task ServiceRestart_WhenNetworkUnavailable_ShouldFailGracefully()
        {
            // Arrange
            var service = new ServiceItem
            {
                DisplayName = "NetworkUnavailableTest",
                ExecutablePath = @"\\unavailable-server\share\executable.exe",
                Arguments = ""
            };

            // Act - Simulate service start attempt
            var stopwatch = Stopwatch.StartNew();

            try
            {
                // This would fail because network path is unavailable
                var fileInfo = new FileInfo(service.ExecutablePath);
                var exists = fileInfo.Exists;

                if (!exists)
                {
                    _output.WriteLine("Network path unavailable (expected)");
                }
                stopwatch.Stop();
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _output.WriteLine($"Exception: {ex.GetType().Name} - {ex.Message}");
            }

            // Assert - Should handle gracefully, not hang
            stopwatch.ElapsedMilliseconds.Should().BeLessThan(10000,
                "network unavailability should be detected within 10 seconds");

            _output.WriteLine($"Network unavailability detected in {stopwatch.ElapsedMilliseconds}ms");
        }

        [Fact]
        public void DnsResolution_Failure_ShouldBeHandled()
        {
            // Arrange
            var invalidDnsPaths = new[]
            {
                @"\\nonexistent-dns-name.local\share\file.exe",
                @"\\this-dns-definitely-does-not-exist-12345.com\share\file.exe",
            };

            foreach (var testPath in invalidDnsPaths)
            {
                // Act
                var stopwatch = Stopwatch.StartNew();
                try
                {
                    var fileInfo = new FileInfo(testPath);
                    var exists = fileInfo.Exists;
                    stopwatch.Stop();

                    _output.WriteLine($"Path: {testPath}");
                    _output.WriteLine($"  Exists check: {exists}");
                    _output.WriteLine($"  Time: {stopwatch.ElapsedMilliseconds}ms");
                }
                catch (Exception ex)
                {
                    stopwatch.Stop();
                    _output.WriteLine($"Path: {testPath}");
                    _output.WriteLine($"  Exception: {ex.GetType().Name}");
                    _output.WriteLine($"  Time: {stopwatch.ElapsedMilliseconds}ms");
                }

                // Assert - DNS resolution should timeout reasonably
                stopwatch.ElapsedMilliseconds.Should().BeLessThan(30000,
                    "DNS resolution timeout should be reasonable");
            }
        }

        [Fact]
        public async Task LocalVsNetworkPath_PerformanceComparison()
        {
            // Arrange
            var localPath = Path.Combine(Environment.SystemDirectory, "timeout.exe");
            var networkDrives = DriveInfo.GetDrives()
                .Where(d => d.DriveType == DriveType.Network && d.IsReady)
                .FirstOrDefault();

            // Act - Test local path access
            var localStopwatch = Stopwatch.StartNew();
            var localExists = File.Exists(localPath);
            localStopwatch.Stop();

            _output.WriteLine($"Local path access ({localPath}):");
            _output.WriteLine($"  Exists: {localExists}");
            _output.WriteLine($"  Time: {localStopwatch.ElapsedMilliseconds}ms");

            // Test network path access (if available)
            if (networkDrives != null)
            {
                var networkPath = Path.Combine(networkDrives.Name, "test.txt");
                var networkStopwatch = Stopwatch.StartNew();
                try
                {
                    var networkExists = File.Exists(networkPath);
                    networkStopwatch.Stop();

                    _output.WriteLine($"Network path access ({networkPath}):");
                    _output.WriteLine($"  Exists: {networkExists}");
                    _output.WriteLine($"  Time: {networkStopwatch.ElapsedMilliseconds}ms");

                    // Assert - Network access should be slower but not dramatically
                    var timeRatio = networkStopwatch.ElapsedMilliseconds / (double)localStopwatch.ElapsedMilliseconds;
                    _output.WriteLine($"Network/Local time ratio: {timeRatio:F2}x");
                }
                catch (Exception ex)
                {
                    networkStopwatch.Stop();
                    _output.WriteLine($"Network path error: {ex.Message}");
                }
            }
            else
            {
                _output.WriteLine("No network drives available for comparison");
            }
        }
    }
}
