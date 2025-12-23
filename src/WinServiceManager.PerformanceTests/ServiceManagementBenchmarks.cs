using System.Collections.Generic;
using System.IO;
using System.Linq;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using WinServiceManager.Models;
using WinServiceManager.Services;
using WinServiceManager.Tests.Fixtures;

namespace WinServiceManager.PerformanceTests
{
    /// <summary>
    /// Performance benchmarks for service management operations
    /// Run with: dotnet run -c Release --project src/WinServiceManager.PerformanceTests
    /// </summary>
    [MemoryDiagnoser]
    [ThreadingDiagnoser]
    public class ServiceManagementBenchmarks
    {
        private List<ServiceItem> _testServices = new();
        private string _testDataPath = string.Empty;
        private JsonDataStorageService _dataStorage = null!;

        [GlobalSetup(Target = nameof(LoadServicesFromStorage))]
        public void SetupLoadBenchmark()
        {
            _testDataPath = Path.Combine(Path.GetTempPath(), $"PerfTest_{Guid.NewGuid()}");
            Directory.CreateDirectory(_testDataPath);

            _dataStorage = new JsonDataStorageService(_testDataPath);

            // Create test services
            _testServices = new List<ServiceItem>();
            for (int i = 0; i < 100; i++)
            {
                var service = new ServiceItem
                {
                    DisplayName = $"PerfTestService_{i}",
                    Description = $"Performance test service {i}",
                    ExecutablePath = Path.Combine(Environment.SystemDirectory, "timeout.exe"),
                    Arguments = "/t 1"
                };
                _testServices.Add(service);
            }

            // Save to storage
            _dataStorage.SaveServicesAsync(_testServices).Wait();
        }

        [GlobalCleanup(Target = nameof(LoadServicesFromStorage))]
        public void CleanupLoadBenchmark()
        {
            _dataStorage?.Dispose();
            if (Directory.Exists(_testDataPath))
            {
                Directory.Delete(_testDataPath, true);
            }
        }

        [Benchmark]
        [Arguments(10)]
        [Arguments(50)]
        [Arguments(100)]
        public async Task<List<ServiceItem>> LoadServicesFromStorage(int count)
        {
            var storagePath = Path.Combine(Path.GetTempPath(), $"PerfTest_Load_{count}_{Guid.NewGuid()}");
            Directory.CreateDirectory(storagePath);

            var storage = new JsonDataStorageService(storagePath);

            try
            {
                // Create services
                var services = new List<ServiceItem>();
                for (int i = 0; i < count; i++)
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
                return await storage.LoadServicesAsync();
            }
            finally
            {
                storage.Dispose();
                if (Directory.Exists(storagePath))
                {
                    Directory.Delete(storagePath, true);
                }
            }
        }

        [Benchmark]
        [Arguments(10)]
        [Arguments(50)]
        [Arguments(100)]
        public async Task CreateMultipleServices(int count)
        {
            var storagePath = Path.Combine(Path.GetTempPath(), $"PerfTest_Create_{count}_{Guid.NewGuid()}");
            Directory.CreateDirectory(storagePath);

            var storage = new JsonDataStorageService(storagePath);

            try
            {
                for (int i = 0; i < count; i++)
                {
                    var service = new ServiceItem
                    {
                        DisplayName = $"CreateTest_{i}",
                        ExecutablePath = Path.Combine(Environment.SystemDirectory, "timeout.exe"),
                        Arguments = "/t 1"
                    };
                    await storage.AddServiceAsync(service);
                }
            }
            finally
            {
                storage.Dispose();
                if (Directory.Exists(storagePath))
                {
                    Directory.Delete(storagePath, true);
                }
            }
        }

        [Benchmark]
        [Arguments(10)]
        [Arguments(50)]
        [Arguments(100)]
        public async Task UpdateMultipleServices(int count)
        {
            var storagePath = Path.Combine(Path.GetTempPath(), $"PerfTest_Update_{count}_{Guid.NewGuid()}");
            Directory.CreateDirectory(storagePath);

            var storage = new JsonDataStorageService(storagePath);

            try
            {
                // Create services first
                var services = new List<ServiceItem>();
                for (int i = 0; i < count; i++)
                {
                    var service = new ServiceItem
                    {
                        DisplayName = $"UpdateTest_{i}",
                        Description = "Initial description",
                        ExecutablePath = Path.Combine(Environment.SystemDirectory, "timeout.exe"),
                        Arguments = "/t 1"
                    };
                    services.Add(service);
                }

                await storage.SaveServicesAsync(services);

                // Update each service
                foreach (var service in services)
                {
                    service.Description = $"Updated description at {DateTime.Now}";
                    await storage.UpdateServiceAsync(service);
                }
            }
            finally
            {
                storage.Dispose();
                if (Directory.Exists(storagePath))
                {
                    Directory.Delete(storagePath, true);
                }
            }
        }

        [Benchmark]
        public async Task GenerateServiceConfiguration()
        {
            var service = new ServiceItem
            {
                DisplayName = "ConfigPerfTest",
                Description = "Performance test for configuration generation",
                ExecutablePath = Path.Combine(Environment.SystemDirectory, "timeout.exe"),
                Arguments = "/t 1",
                WorkingDirectory = Environment.SystemDirectory
            };

            // Generate config 100 times
            for (int i = 0; i < 100; i++)
            {
                var config = service.GenerateWinSWConfig();
            }
        }

        [Benchmark]
        [Arguments(10)]
        [Arguments(50)]
        [Arguments(100)]
        public async Task ValidateDependencies(int count)
        {
            var storagePath = Path.Combine(Path.GetTempPath(), $"PerfTest_Dep_{count}_{Guid.NewGuid()}");
            Directory.CreateDirectory(storagePath);

            var storage = new JsonDataStorageService(storagePath);

            try
            {
                // Create dependency chain: Service0 -> Service1 -> ... -> ServiceN
                var services = new List<ServiceItem>();
                for (int i = 0; i < count; i++)
                {
                    var service = new ServiceItem
                    {
                        DisplayName = $"DepTest_{i}",
                        ExecutablePath = Path.Combine(Environment.SystemDirectory, "timeout.exe"),
                        Arguments = "/t 1"
                    };

                    if (i > 0)
                    {
                        service.Dependencies.Add(services[i - 1].Id);
                    }

                    services.Add(service);
                }

                await storage.SaveServicesAsync(services);

                // Validate dependencies for the last service
                var validator = new ServiceDependencyValidator(null);
                await validator.ValidateDependenciesAsync(services.Last());
            }
            finally
            {
                storage.Dispose();
                if (Directory.Exists(storagePath))
                {
                    Directory.Delete(storagePath, true);
                }
            }
        }

        [Benchmark]
        public async Task ValidatePathSecurity()
        {
            var testPaths = new[]
            {
                "C:\\Windows\\System32\\timeout.exe",
                "C:\\Program Files\\MyApp\\app.exe",
                "..\\..\\Windows\\System32\\cmd.exe",
                "\\\\evilserver\\share\\malware.exe",
                "C:\\MyFolder\\test.exe",
                "CON.exe",
                "C:\\Valid\\Path\\to\\app.exe"
            };

            foreach (var path in testPaths)
            {
                PathValidator.IsValidPath(path);
                CommandValidator.IsValidExecutable(path);
            }
        }

        [Benchmark]
        public async Task ServiceStatusMonitor()
        {
            // Simulate monitoring 100 services
            var services = new List<ServiceItem>();
            for (int i = 0; i < 100; i++)
            {
                var service = new ServiceItem
                {
                    DisplayName = $"MonitorTest_{i}",
                    ExecutablePath = Path.Combine(Environment.SystemDirectory, "timeout.exe"),
                    Arguments = "/t 1"
                };
                services.Add(service);
            }

            // Simulate status checks
            foreach (var service in services)
            {
                // In real scenario, this would check actual service status
                var status = service.Status;
            }
        }
    }

    /// <summary>
    /// Entry point for running benchmarks
    /// </summary>
    public class Program
    {
        public static void Main(string[] args)
        {
            BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
        }
    }
}
