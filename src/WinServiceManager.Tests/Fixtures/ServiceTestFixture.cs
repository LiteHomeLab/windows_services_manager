using System;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using WinServiceManager.Models;
using WinServiceManager.Services;
using Xunit;

namespace WinServiceManager.Tests.Fixtures
{
    /// <summary>
    /// Service test fixture - provides isolated test environment for service integration tests
    /// </summary>
    public class ServiceTestFixture : IDisposable
    {
        /// <summary>
        /// Gets the test services directory
        /// </summary>
        public string TestServicesDirectory { get; }

        /// <summary>
        /// Gets the test WinSW executable path
        /// </summary>
        public string TestWinSWPath { get; }

        /// <summary>
        /// Gets the templates directory
        /// </summary>
        public string TemplatesDirectory { get; }

        /// <summary>
        /// Gets a test logger instance
        /// </summary>
        public ILogger<TestLogger> TestLogger { get; }

        /// <summary>
        /// Gets a mock data storage service for testing
        /// </summary>
        public MockDataStorageService MockDataStorage { get; }

        public ServiceTestFixture()
        {
            // Create isolated test directory
            TestServicesDirectory = Path.Combine(Path.GetTempPath(),
                $"WinServiceManagerTests_{Guid.NewGuid()}");
            Directory.CreateDirectory(TestServicesDirectory);

            // Setup templates directory path
            TemplatesDirectory = Path.Combine(
                Path.GetDirectoryName(GetType().Assembly.Location) ?? AppDomain.CurrentDomain.BaseDirectory,
                "..", "..", "..", "..", "WinServiceManager", "templates");
            TestWinSWPath = Path.Combine(TemplatesDirectory, "WinSW-x64.exe");

            // Initialize test logger
            TestLogger = new LoggerFactory().CreateLogger<TestLogger>();

            // Initialize mock data storage
            MockDataStorage = new MockDataStorageService(TestServicesDirectory);
        }

        /// <summary>
        /// Creates a test service with valid default values
        /// </summary>
        public ServiceItem CreateTestService(string? displayName = null, string? executablePath = null)
        {
            // Use a simple executable that exists on all Windows systems
            var testExe = executablePath ?? Path.Combine(Environment.SystemDirectory, "timeout.exe");

            return new ServiceItem
            {
                DisplayName = displayName ?? $"TestService_{Guid.NewGuid()}",
                Description = "Test service for integration testing",
                ExecutablePath = testExe,
                Arguments = "/t 1", // timeout for 1 second
                WorkingDirectory = Environment.SystemDirectory
            };
        }

        /// <summary>
        /// Creates a test service with a specific executable for testing
        /// </summary>
        public ServiceItem CreateTestServiceWithExecutable(string executablePath, string? displayName = null)
        {
            return new ServiceItem
            {
                DisplayName = displayName ?? $"TestService_{Guid.NewGuid()}",
                Description = "Test service for integration testing",
                ExecutablePath = executablePath,
                Arguments = string.Empty,
                WorkingDirectory = Path.GetDirectoryName(executablePath) ?? string.Empty
            };
        }

        /// <summary>
        /// Checks if running with administrator privileges
        /// </summary>
        public bool IsAdministrator()
        {
            try
            {
                using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
                var principal = new System.Security.Principal.WindowsPrincipal(identity);
                return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }

        public void Dispose()
        {
            // Clean up test environment
            try
            {
                if (Directory.Exists(TestServicesDirectory))
                {
                    // Try to stop any running test services first
                    foreach (var serviceDir in Directory.GetDirectories(TestServicesDirectory))
                    {
                        try
                        {
                            var serviceName = Path.GetFileName(serviceDir);
                            var exePath = Path.Combine(serviceDir, $"{serviceName}.exe");
                            if (File.Exists(exePath))
                            {
                                // Try to stop the service
                                using var process = new System.Diagnostics.Process();
                                process.StartInfo = new System.Diagnostics.ProcessStartInfo
                                {
                                    FileName = exePath,
                                    Arguments = "stop",
                                    UseShellExecute = false,
                                    CreateNoWindow = true
                                };
                                process.Start();
                                process.WaitForExit(5000);
                            }
                        }
                        catch
                        {
                            // Ignore cleanup errors
                        }
                    }

                    // Then delete the directory
                    Directory.Delete(TestServicesDirectory, true);
                }
            }
            catch
            {
                // Ignore cleanup errors in test fixture
            }
        }
    }

    /// <summary>
    /// Mock data storage service for testing
    /// </summary>
    public class MockDataStorageService : IDataStorageService, System.IDisposable
    {
        private readonly string _storageDirectory;
        private readonly string _storageFile;

        public MockDataStorageService(string baseDirectory)
        {
            _storageDirectory = Path.Combine(baseDirectory, "Storage");
            Directory.CreateDirectory(_storageDirectory);
            _storageFile = Path.Combine(_storageDirectory, "services.json");
        }

        public Task<List<ServiceItem>> LoadServicesAsync()
        {
            try
            {
                if (!File.Exists(_storageFile))
                {
                    return Task.FromResult(new List<ServiceItem>());
                }

                var json = File.ReadAllText(_storageFile);
                var services = System.Text.Json.JsonSerializer.Deserialize<List<ServiceItem>>(json);
                return Task.FromResult(services ?? new List<ServiceItem>());
            }
            catch
            {
                return Task.FromResult(new List<ServiceItem>());
            }
        }

        public Task SaveServicesAsync(List<ServiceItem> services)
        {
            var options = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
            var json = System.Text.Json.JsonSerializer.Serialize(services, options);
            File.WriteAllText(_storageFile, json);
            return Task.CompletedTask;
        }

        public Task<ServiceItem?> GetServiceAsync(string id)
        {
            var services = LoadServicesAsync().Result;
            var service = services.FirstOrDefault(s => s.Id == id);
            return Task.FromResult(service);
        }

        public Task<ServiceItem?> LoadServiceAsync(string id)
        {
            return GetServiceAsync(id);
        }

        public Task AddServiceAsync(ServiceItem service)
        {
            var services = LoadServicesAsync().Result;
            services.Add(service);
            return SaveServicesAsync(services);
        }

        public Task UpdateServiceAsync(ServiceItem service)
        {
            var services = LoadServicesAsync().Result;
            var index = services.FindIndex(s => s.Id == service.Id);
            if (index >= 0)
            {
                services[index] = service;
                return SaveServicesAsync(services);
            }
            return Task.CompletedTask;
        }

        public Task DeleteServiceAsync(string serviceId)
        {
            var services = LoadServicesAsync().Result;
            var service = services.FirstOrDefault(s => s.Id == serviceId);
            if (service != null)
            {
                services.Remove(service);
                return SaveServicesAsync(services);
            }
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            try
            {
                if (File.Exists(_storageFile))
                {
                    File.Delete(_storageFile);
                }
                if (Directory.Exists(_storageDirectory))
                {
                    Directory.Delete(_storageDirectory, true);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    /// <summary>
    /// Simple test logger class for logging
    /// </summary>
    public class TestLogger
    {
    }
}
