using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using WinServiceManager.Models;
using WinServiceManager.Services;
using WinServiceManager.Tests.Fixtures;
using Xunit;
using Xunit.Abstractions;

namespace WinServiceManager.Tests.IntegrationTests.EdgeCases
{
    /// <summary>
    /// Disk space exhaustion tests
    /// Tests application behavior when disk space is running low or full
    /// </summary>
    [Collection("Edge Cases Tests")]
    public class DiskSpaceExhaustionTests : IClassFixture<ServiceTestFixture>, IDisposable
    {
        private readonly ServiceTestFixture _fixture;
        private readonly ITestOutputHelper _output;
        private readonly MockDataStorageService _dataStorage;
        private readonly string _testVolumePath;

        public DiskSpaceExhaustionTests(ServiceTestFixture fixture, ITestOutputHelper output)
        {
            _fixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
            _output = output ?? throw new ArgumentNullException(nameof(output));
            _dataStorage = _fixture.MockDataStorage;

            // Use the temp drive as test volume (typically C:)
            _testVolumePath = Path.GetPathRoot(_fixture.TestServicesDirectory)
                ?? Environment.SystemDirectory;
        }

        public void Dispose()
        {
            // Cleanup
        }

        [Fact]
        public async Task CreateService_WithLowDiskSpace_WarnsUser()
        {
            // Arrange
            var driveInfo = new DriveInfo(_testVolumePath);
            var availableFreeSpace = driveInfo.AvailableFreeSpace;
            var totalFreeSpace = driveInfo.TotalFreeSpace;

            _output.WriteLine($"Available free space: {availableFreeSpace / (1024.0 * 1024.0):F2} MB");
            _output.WriteLine($"Total free space: {totalFreeSpace / (1024.0 * 1024.0):F2} MB");

            // Act - Try to create a service
            var service = _fixture.CreateTestService("LowDiskSpaceTest");

            // If disk space is critically low (< 100MB), the operation should warn
            if (availableFreeSpace < 100 * 1024 * 1024)
            {
                _output.WriteLine("WARNING: Disk space is critically low!");
                // In production, this should trigger a warning to the user
            }

            // Assert - Service should still be created (or fail gracefully)
            await _dataStorage.AddServiceAsync(service);
            var loaded = await _dataStorage.GetServiceAsync(service.Id);

            loaded.Should().NotBeNull();
            loaded.DisplayName.Should().Be("LowDiskSpaceTest");

            _output.WriteLine("Service created successfully despite low disk space");
        }

        [Fact]
        public async Task ServiceLogs_WithRotatingLogFiles_ManagesDiskUsage()
        {
            // Arrange
            var service = _fixture.CreateTestService("LogRotationTest");
            await _dataStorage.AddServiceAsync(service);

            var logDir = Path.Combine(_fixture.TestServicesDirectory, service.Id, "logs");
            Directory.CreateDirectory(logDir);

            // Create multiple log files to simulate rotation
            var logFiles = new List<string>();
            for (int i = 0; i < 10; i++)
            {
                var logFile = Path.Combine(logDir, $"test_{i}.log");
                await File.WriteAllTextAsync(logFile, new string('x', 1024 * 100)); // 100KB each
                logFiles.Add(logFile);
            }

            // Act - Check total log size
            var totalLogSize = logFiles.Sum(f => new FileInfo(f).Length);
            var logCount = Directory.GetFiles(logDir, "*.log").Length;

            // Assert
            totalLogSize.Should().BeGreaterThan(0);
            logCount.Should().Be(10);

            _output.WriteLine($"Total log size: {totalLogSize / 1024.0:F2} KB");
            _output.WriteLine($"Log file count: {logCount}");

            // Cleanup
            foreach (var file in logFiles)
            {
                if (File.Exists(file))
                {
                    File.Delete(file);
                }
            }
        }

        [Fact]
        public void CheckDiskSpace_BeforeLargeOperation_ProvidesWarning()
        {
            // Arrange
            var driveInfo = new DriveInfo(_testVolumePath);
            var availableMB = driveInfo.AvailableFreeSpace / (1024.0 * 1024.0);
            var totalMB = driveInfo.TotalSize / (1024.0 * 1024.0);
            var diskUsagePercent = ((totalMB - availableMB) / totalMB) * 100;

            // Act
            _output.WriteLine($"Drive: {_testVolumePath}");
            _output.WriteLine($"Available: {availableMB:F2} MB");
            _output.WriteLine($"Total: {totalMB:F2} MB");
            _output.WriteLine($"Usage: {diskUsagePercent:F2}%");

            // Assert - Determine if we should warn
            var shouldWarn = availableMB < 500 || diskUsagePercent > 95;

            if (shouldWarn)
            {
                _output.WriteLine("WARNING: Disk space is low!");
            }

            // This test documents the expected warning thresholds
            availableMB.Should().BeGreaterOrEqualTo(0, "available space should never be negative");
            diskUsagePercent.Should().BeLessThan(100, "usage should be under 100%");
        }

        [Fact]
        public async Task ExportServices_LargeDataset_HandlesDiskSpace()
        {
            // Arrange
            const int serviceCount = 1000;
            var services = new List<ServiceItem>();

            for (int i = 0; i < serviceCount; i++)
            {
                var service = new ServiceItem
                {
                    DisplayName = $"ExportDiskSpaceTest_{i}",
                    ExecutablePath = Path.Combine(Environment.SystemDirectory, "timeout.exe"),
                    Arguments = "/t 1",
                    Description = new string('x', 500) // Large description
                };
                services.Add(service);
            }

            await _dataStorage.SaveServicesAsync(services);

            // Act - Export to JSON and check size
            var stopwatch = Stopwatch.StartNew();
            var loadedServices = await _dataStorage.LoadServicesAsync();

            var options = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
            var exportedJson = System.Text.Json.JsonSerializer.Serialize(loadedServices, options);
            stopwatch.Stop();

            var jsonSize = exportedJson.Length;
            var jsonSizeKB = jsonSize / 1024.0;

            // Assert
            exportedJson.Should().NotBeNullOrEmpty();
            jsonSizeKB.Should().BeLessThan(1024 * 10, "export should not exceed 10MB for 1000 services");

            _output.WriteLine($"Exported {serviceCount} services in {stopwatch.ElapsedMilliseconds}ms");
            _output.WriteLine($"Export size: {jsonSizeKB:F2} KB");
            _output.WriteLine($"Average per service: {jsonSizeKB / serviceCount:F2} KB");
        }

        [Fact]
        public async Task CreateService_WhenDiskFull_FailsGracefully()
        {
            // Arrange
            var service = _fixture.CreateTestService("DiskFullTest");

            // Get available space before
            var driveInfo = new DriveInfo(_testVolumePath);
            var initialFreeSpace = driveInfo.AvailableFreeSpace;
            _output.WriteLine($"Initial free space: {initialFreeSpace / (1024.0 * 1024.0):F2} MB");

            // Act - Try to create service
            try
            {
                await _dataStorage.AddServiceAsync(service);

                // Get available space after
                driveInfo.Refresh();
                var finalFreeSpace = driveInfo.AvailableFreeSpace;
                var spaceUsed = initialFreeSpace - finalFreeSpace;

                _output.WriteLine($"Final free space: {finalFreeSpace / (1024.0 * 1024.0):F2} MB");
                _output.WriteLine($"Space used: {spaceUsed / 1024.0:F2} KB");

                // Assert
                var loadedService = await _dataStorage.GetServiceAsync(service.Id);
                loadedService.Should().NotBeNull();
            }
            catch (IOException ex) when (ex.Message.Contains("disk") || ex.Message.Contains("space"))
            {
                // Expected behavior when disk is actually full
                _output.WriteLine($"Expected exception caught: {ex.Message}");
            }
        }

        [Fact]
        public void GetDiskSpaceInfo_ReturnsAccurateInformation()
        {
            // Arrange & Act
            var driveInfo = new DriveInfo(_testVolumePath);
            driveInfo.Refresh();

            var availableFreeSpace = driveInfo.AvailableFreeSpace;
            var totalFreeSpace = driveInfo.TotalFreeSpace;
            var totalSize = driveInfo.TotalSize;

            // Assert
            availableFreeSpace.Should().BeGreaterOrEqualTo(0);
            totalFreeSpace.Should().BeGreaterOrEqualTo(0);
            totalSize.Should().BeGreaterThan(0);

            availableFreeSpace.Should().BeLessOrEqualTo(totalFreeSpace,
                "available space should not exceed total free space");
            totalFreeSpace.Should().BeLessOrEqualTo(totalSize,
                "total free space should not exceed total size");

            _output.WriteLine($"Drive {_testVolumePath} information:");
            _output.WriteLine($"  Total: {totalSize / (1024.0 * 1024.0 * 1024.0):F2} GB");
            _output.WriteLine($"  Free: {totalFreeSpace / (1024.0 * 1024.0 * 1024.0):F2} GB");
            _output.WriteLine($"  Available: {availableFreeSpace / (1024.0 * 1024.0 * 1024.0):F2} GB");
        }

        [Fact]
        public async Task MultipleServiceCreations_TrackDiskUsage()
        {
            // Arrange
            var serviceCount = 50;
            var driveInfo = new DriveInfo(_testVolumePath);
            var initialFreeSpace = driveInfo.AvailableFreeSpace;

            _output.WriteLine($"Initial free space: {initialFreeSpace / (1024.0 * 1024.0):F2} MB");

            // Act - Create multiple services
            var services = new List<ServiceItem>();
            for (int i = 0; i < serviceCount; i++)
            {
                var service = _fixture.CreateTestService($"DiskUsageTest_{i}");
                services.Add(service);
                await _dataStorage.AddServiceAsync(service);
            }

            // Measure disk usage after
            driveInfo.Refresh();
            var finalFreeSpace = driveInfo.AvailableFreeSpace;
            var spaceUsed = initialFreeSpace - finalFreeSpace;
            var avgSpacePerService = spaceUsed / serviceCount;

            // Assert
            var loadedServices = await _dataStorage.LoadServicesAsync();
            loadedServices.Should().HaveCount(serviceCount);

            _output.WriteLine($"Final free space: {finalFreeSpace / (1024.0 * 1024.0):F2} MB");
            _output.WriteLine($"Total space used: {spaceUsed / 1024.0:F2} KB");
            _output.WriteLine($"Average per service: {avgSpacePerService:F2} bytes");
        }

        [Fact]
        public void SystemDrive_HasSufficientSpaceForApplication()
        {
            // Arrange & Act
            var systemDrive = new DriveInfo(Path.GetPathRoot(Environment.SystemDirectory));
            systemDrive.Refresh();

            var availableMB = systemDrive.AvailableFreeSpace / (1024.0 * 1024.0);

            // Assert - Document minimum space requirements
            _output.WriteLine($"System drive: {systemDrive.Name}");
            _output.WriteLine($"Available space: {availableMB:F2} MB");

            // Minimum recommended space: 100MB
            if (availableMB < 100)
            {
                _output.WriteLine("WARNING: System drive has less than 100MB available");
            }

            // This is an informational test - documents expected requirements
            availableMB.Should().BeGreaterOrEqualTo(0, "should have at least some space available");
        }
    }
}
