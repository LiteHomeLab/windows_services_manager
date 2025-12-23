using System;
using System.Collections.Generic;
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

namespace WinServiceManager.Tests.IntegrationTests.Performance
{
    /// <summary>
    /// Large scale service management tests
    /// Tests the performance and behavior when managing many services
    /// </summary>
    [Collection("Performance Tests")]
    public class LargeScaleServiceManagementTests : IClassFixture<ServiceTestFixture>, IDisposable
    {
        private readonly ServiceTestFixture _fixture;
        private readonly ITestOutputHelper _output;
        private readonly MockDataStorageService _dataStorage;

        public LargeScaleServiceManagementTests(ServiceTestFixture fixture, ITestOutputHelper output)
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
        public async Task Create100Services_CompletesWithinTimeLimit()
        {
            // Arrange
            const int serviceCount = 100;
            var services = new List<ServiceItem>();
            var stopwatch = Stopwatch.StartNew();

            // Act
            for (int i = 0; i < serviceCount; i++)
            {
                var service = _fixture.CreateTestService($"BulkCreate_{i}");
                services.Add(service);
                await _dataStorage.AddServiceAsync(service);
            }

            stopwatch.Stop();

            // Assert
            services.Should().HaveCount(serviceCount);
            _output.WriteLine($"Created {serviceCount} services in {stopwatch.ElapsedMilliseconds}ms");
            _output.WriteLine($"Average time per service: {stopwatch.ElapsedMilliseconds / (double)serviceCount:F2}ms");

            // Performance assertion: Should complete in reasonable time
            stopwatch.ElapsedMilliseconds.Should().BeLessThan(5000, "creating 100 services should complete within 5 seconds");
        }

        [Fact]
        public async Task Load1000Services_FromStorage_CompletesQuickly()
        {
            // Arrange
            const int serviceCount = 1000;
            var logger = new LoggerFactory().CreateLogger<JsonDataStorageService>();
            var storage = new JsonDataStorageService(logger);

            // Pre-populate storage
            var services = new List<ServiceItem>();
            for (int i = 0; i < serviceCount; i++)
            {
                var service = new ServiceItem
                {
                    DisplayName = $"LoadTest_{i}",
                    ExecutablePath = Path.Combine(Environment.SystemDirectory, "timeout.exe"),
                    Arguments = "/t 1"
                };
                services.Add(service);
            }

            await storage.SaveServicesAsync(services);

            // Act
            var stopwatch = Stopwatch.StartNew();
            var loadedServices = await storage.LoadServicesAsync();
            stopwatch.Stop();

            // Assert
            loadedServices.Should().HaveCount(serviceCount);
            _output.WriteLine($"Loaded {serviceCount} services in {stopwatch.ElapsedMilliseconds}ms");

            // Performance assertion
            stopwatch.ElapsedMilliseconds.Should().BeLessThan(2000, "loading 1000 services should complete within 2 seconds");
        }

        [Fact]
        public async Task RefreshAllServices_With100Services_UpdateTimeAcceptable()
        {
            // Arrange
            const int serviceCount = 100;
            var services = new List<ServiceItem>();

            for (int i = 0; i < serviceCount; i++)
            {
                var service = _fixture.CreateTestService($"RefreshTest_{i}");
                services.Add(service);
                await _dataStorage.AddServiceAsync(service);
            }

            // Act - Simulate refresh by loading all services
            var stopwatch = Stopwatch.StartNew();
            var loadedServices = await _dataStorage.LoadServicesAsync();

            // Update status for each service
            foreach (var service in loadedServices)
            {
                // In real scenario, this would check actual service status
                service.Status = ServiceStatus.Stopped;
            }

            stopwatch.Stop();

            // Assert
            loadedServices.Should().HaveCount(serviceCount);
            _output.WriteLine($"Refreshed {serviceCount} services in {stopwatch.ElapsedMilliseconds}ms");
            _output.WriteLine($"Average time per service: {stopwatch.ElapsedMilliseconds / (double)serviceCount:F2}ms");

            // Performance assertion
            stopwatch.ElapsedMilliseconds.Should().BeLessThan(1000, "refreshing 100 services should complete within 1 second");
        }

        [Fact]
        public async Task SearchServices_LargeDataset_FastResponse()
        {
            // Arrange
            const int serviceCount = 500;
            var services = new List<ServiceItem>();

            // Create services with searchable names
            for (int i = 0; i < serviceCount; i++)
            {
                var service = new ServiceItem
                {
                    DisplayName = i % 10 == 0 ? $"Production_Service_{i}" : $"Development_Service_{i}",
                    ExecutablePath = Path.Combine(Environment.SystemDirectory, "timeout.exe"),
                    Arguments = "/t 1"
                };
                services.Add(service);
            }

            await _dataStorage.SaveServicesAsync(services);

            // Act - Search for specific services
            var stopwatch = Stopwatch.StartNew();
            var loadedServices = await _dataStorage.LoadServicesAsync();
            var searchResults = loadedServices
                .Where(s => s.DisplayName.Contains("Production"))
                .ToList();
            stopwatch.Stop();

            // Assert
            searchResults.Should().HaveCount(serviceCount / 10, "should find all production services");
            _output.WriteLine($"Search completed in {stopwatch.ElapsedMilliseconds}ms");

            // Performance assertion
            stopwatch.ElapsedMilliseconds.Should().BeLessThan(500, "searching 500 services should complete within 500ms");
        }

        [Fact]
        public async Task ExportServices_LargeDataset_CompletesSuccessfully()
        {
            // Arrange
            const int serviceCount = 200;
            var services = new List<ServiceItem>();

            for (int i = 0; i < serviceCount; i++)
            {
                var service = _fixture.CreateTestService($"ExportTest_{i}");
                services.Add(service);
                await _dataStorage.AddServiceAsync(service);
            }

            // Act - Simulate export by serializing to JSON
            var stopwatch = Stopwatch.StartNew();
            var loadedServices = await _dataStorage.LoadServicesAsync();

            var options = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
            var exportedJson = System.Text.Json.JsonSerializer.Serialize(loadedServices, options);
            stopwatch.Stop();

            // Assert
            exportedJson.Should().NotBeNullOrEmpty();
            _output.WriteLine($"Exported {serviceCount} services ({exportedJson.Length} chars) in {stopwatch.ElapsedMilliseconds}ms");

            // Performance assertion
            stopwatch.ElapsedMilliseconds.Should().BeLessThan(2000, "exporting 200 services should complete within 2 seconds");
        }

        [Fact]
        public async Task MemoryUsage_WithManyServices_StaysWithinBounds()
        {
            // Arrange
            const int serviceCount = 1000;
            var initialMemory = GC.GetTotalMemory(true);

            // Act - Create many services in memory
            var services = new List<ServiceItem>();
            for (int i = 0; i < serviceCount; i++)
            {
                var service = new ServiceItem
                {
                    DisplayName = $"MemoryTest_{i}",
                    Description = new string('x', 100), // Add some data
                    ExecutablePath = Path.Combine(Environment.SystemDirectory, "timeout.exe"),
                    Arguments = "/t 1"
                };
                services.Add(service);
            }

            var finalMemory = GC.GetTotalMemory(true);
            var memoryUsed = finalMemory - initialMemory;
            var memoryPerService = memoryUsed / serviceCount;

            // Assert
            _output.WriteLine($"Memory used for {serviceCount} services: {memoryUsed / 1024:F2} KB");
            _output.WriteLine($"Average memory per service: {memoryPerService} bytes");

            // Performance assertion: Each service should use less than 10KB
            memoryPerService.Should().BeLessThan(10 * 1024, "each service should use less than 10KB of memory");
        }

        [Fact]
        public async Task StatusMonitor_With100Services_CPUUsageAcceptable()
        {
            // Arrange
            const int serviceCount = 100;
            var services = new List<ServiceItem>();

            for (int i = 0; i < serviceCount; i++)
            {
                var service = _fixture.CreateTestService($"MonitorTest_{i}");
                services.Add(service);
            }

            // Act - Simulate status monitoring cycles
            var stopwatch = Stopwatch.StartNew();
            int cycleCount = 10;

            for (int cycle = 0; cycle < cycleCount; cycle++)
            {
                foreach (var service in services)
                {
                    // Simulate status check
                    var status = service.Status;
                }

                // Small delay to simulate monitoring interval
                await Task.Delay(10);
            }

            stopwatch.Stop();

            // Assert
            var totalTime = stopwatch.ElapsedMilliseconds;
            var avgTimePerCycle = totalTime / cycleCount;
            var avgTimePerServicePerCycle = avgTimePerCycle / (double)serviceCount;

            _output.WriteLine($"Total time for {cycleCount} cycles: {totalTime}ms");
            _output.WriteLine($"Average time per cycle: {avgTimePerCycle}ms");
            _output.WriteLine($"Average time per service per cycle: {avgTimePerServicePerCycle:F3}ms");

            // Performance assertion
            avgTimePerServicePerCycle.Should().BeLessThan(1, "checking each service should take less than 1ms on average");
        }

        [Fact]
        public async Task DatabaseOperations_LargeDataSet_PerformanceBenchmarks()
        {
            // Arrange
            const int serviceCount = 500;
            var logger = new LoggerFactory().CreateLogger<JsonDataStorageService>();
            var storage = new JsonDataStorageService(logger);

            var services = new List<ServiceItem>();

            // Benchmark: Create operations
            var createStopwatch = Stopwatch.StartNew();
            for (int i = 0; i < serviceCount; i++)
            {
                var service = new ServiceItem
                {
                    DisplayName = $"DBTest_{i}",
                    ExecutablePath = Path.Combine(Environment.SystemDirectory, "timeout.exe"),
                    Arguments = "/t 1"
                };
                services.Add(service);
                await storage.AddServiceAsync(service);
            }
            createStopwatch.Stop();

            // Benchmark: Read operations
            var readStopwatch = Stopwatch.StartNew();
            var loadedServices = await storage.LoadServicesAsync();
            readStopwatch.Stop();

            // Benchmark: Update operations
            var updateStopwatch = Stopwatch.StartNew();
            foreach (var service in loadedServices.Take(100)) // Update 100 services
            {
                service.Description = $"Updated at {DateTime.Now}";
                await storage.UpdateServiceAsync(service);
            }
            updateStopwatch.Stop();

            // Benchmark: Delete operations
            var deleteStopwatch = Stopwatch.StartNew();
            foreach (var service in services.Take(50)) // Delete 50 services
            {
                await storage.DeleteServiceAsync(service.Id);
            }
            deleteStopwatch.Stop();

            // Assert
            _output.WriteLine($"Database Performance Results for {serviceCount} services:");
            _output.WriteLine($"  Create: {createStopwatch.ElapsedMilliseconds}ms ({createStopwatch.ElapsedMilliseconds / (double)serviceCount:F2}ms per operation)");
            _output.WriteLine($"  Read: {readStopwatch.ElapsedMilliseconds}ms");
            _output.WriteLine($"  Update: {updateStopwatch.ElapsedMilliseconds}ms ({updateStopwatch.ElapsedMilliseconds / 100.0:F2}ms per operation)");
            _output.WriteLine($"  Delete: {deleteStopwatch.ElapsedMilliseconds}ms ({deleteStopwatch.ElapsedMilliseconds / 50.0:F2}ms per operation)");

            // Performance assertions
            createStopwatch.ElapsedMilliseconds.Should().BeLessThan(5000, "creating 500 services should complete within 5 seconds");
            readStopwatch.ElapsedMilliseconds.Should().BeLessThan(1000, "reading 500 services should complete within 1 second");
        }

        [Fact]
        public async Task ConcurrentOperations_MultipleThreads_ThreadSafe()
        {
            // Arrange
            const int serviceCount = 50;
            const int threadCount = 5;
            var servicesPerThread = serviceCount / threadCount;

            var logger = new LoggerFactory().CreateLogger<JsonDataStorageService>();
            var storage = new JsonDataStorageService(logger);

            // Act - Run concurrent operations
            var stopwatch = Stopwatch.StartNew();
            var tasks = new List<Task>();

            for (int t = 0; t < threadCount; t++)
            {
                int threadId = t;
                var task = Task.Run(async () =>
                {
                    for (int i = 0; i < servicesPerThread; i++)
                    {
                        var service = new ServiceItem
                        {
                            DisplayName = $"Concurrent_T{threadId}_S{i}",
                            ExecutablePath = Path.Combine(Environment.SystemDirectory, "timeout.exe"),
                            Arguments = "/t 1"
                        };
                        await storage.AddServiceAsync(service);
                    }
                });
                tasks.Add(task);
            }

            await Task.WhenAll(tasks);
            stopwatch.Stop();

            // Assert
            var finalServices = await storage.LoadServicesAsync();
            finalServices.Should().HaveCount(serviceCount, "all concurrent operations should complete successfully");

            _output.WriteLine($"Concurrent operations completed in {stopwatch.ElapsedMilliseconds}ms");
            _output.WriteLine($"  {threadCount} threads created {serviceCount} services");
            _output.WriteLine($"  Average: {stopwatch.ElapsedMilliseconds / (double)serviceCount:F2}ms per service");
        }
    }
}
