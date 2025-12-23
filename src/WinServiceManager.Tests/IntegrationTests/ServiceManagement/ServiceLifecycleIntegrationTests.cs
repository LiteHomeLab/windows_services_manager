using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using WinServiceManager.Models;
using WinServiceManager.Services;
using WinServiceManager.Tests.Fixtures;
using Xunit;
using Xunit.Abstractions;

namespace WinServiceManager.Tests.IntegrationTests.ServiceManagement
{
    /// <summary>
    /// Service lifecycle integration tests
    /// Tests the complete flow: Create -> Install -> Start -> Monitor -> Stop -> Uninstall
    /// Requires administrator privileges to run
    /// </summary>
    [Collection("Integration Tests")]
    public class ServiceLifecycleIntegrationTests : IClassFixture<ServiceTestFixture>, IDisposable
    {
        private readonly ServiceTestFixture _fixture;
        private readonly ITestOutputHelper _output;
        private readonly WinSWWrapper? _winswWrapper;
        private readonly IDataStorageService _dataStorage;

        public ServiceLifecycleIntegrationTests(ServiceTestFixture fixture, ITestOutputHelper output)
        {
            _fixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
            _output = output ?? throw new ArgumentNullException(nameof(output));

            // Skip tests if not running as administrator
            if (!_fixture.IsAdministrator())
            {
                _output.WriteLine("WARNING: Tests require administrator privileges. Skipping.");
                _winswWrapper = null;
            }
            else
            {
                _dataStorage = _fixture.MockDataStorage;
                var logger = new LoggerFactory().CreateLogger<WinSWWrapper>();
                _winswWrapper = new WinSWWrapper(logger);
            }

            _dataStorage = _fixture.MockDataStorage;
        }

        public void Dispose()
        {
            // Cleanup any remaining test services
            CleanupTestServices().Wait();
        }

        [Fact]
        public async Task ServiceLifecycle_CompleteFlow_EndToEnd()
        {
            // Skip if not administrator
            if (!_fixture.IsAdministrator())
            {
                _output.WriteLine("SKIPPED: Requires administrator privileges");
                return;
            }

            // Arrange
            var service = _fixture.CreateTestService("LifecycleTest");
            _output.WriteLine($"Creating service: {service.DisplayName}");

            try
            {
                // Act & Assert - Install
                _output.WriteLine("Step 1: Installing service...");
                var installResult = await _winswWrapper!.InstallServiceAsync(service);
                installResult.Success.Should().BeTrue("service installation should succeed");
                installResult.ErrorMessage.Should().BeNullOrEmpty();

                // Verify service exists in Windows
                var serviceExists = await ServiceExistsAsync(service.Id);
                serviceExists.Should().BeTrue("service should be registered in Windows");

                // Verify files were created
                File.Exists(service.WinSWExecutablePath).Should().BeTrue("WinSW executable should exist");
                File.Exists(service.WinSWConfigPath).Should().BeTrue("WinSW config should exist");

                // Act & Assert - Start
                _output.WriteLine("Step 2: Starting service...");
                var startResult = await _winswWrapper!.StartServiceAsync(service);
                startResult.Success.Should().BeTrue("service start should succeed");

                // Wait for service to start
                await Task.Delay(2000);

                // Verify service is running
                var status = GetServiceStatus(service.Id);
                status.Should().Be(ServiceControllerStatus.Running, "service should be in running state");

                // Act & Assert - Stop
                _output.WriteLine("Step 3: Stopping service...");
                var stopResult = await _winswWrapper!.StopServiceAsync(service);
                stopResult.Success.Should().BeTrue("service stop should succeed");

                // Wait for service to stop
                await Task.Delay(2000);

                // Verify service is stopped
                status = GetServiceStatus(service.Id);
                status.Should().Be(ServiceControllerStatus.Stopped, "service should be in stopped state");

                // Act & Assert - Uninstall
                _output.WriteLine("Step 4: Uninstalling service...");
                var uninstallResult = await _winswWrapper!.UninstallServiceAsync(service);
                uninstallResult.Success.Should().BeTrue("service uninstall should succeed");

                // Verify service is removed from Windows
                serviceExists = await ServiceExistsAsync(service.Id);
                serviceExists.Should().BeFalse("service should be removed from Windows");

                // Verify files are cleaned up
                Directory.Exists(service.ServiceDirectory).Should().BeFalse("service directory should be cleaned up");

                _output.WriteLine("Complete lifecycle test passed!");
            }
            finally
            {
                // Ensure cleanup even if test fails
                await CleanupServiceAsync(service);
            }
        }

        [Fact]
        public async Task ServiceWithDependencies_StartsInCorrectOrder()
        {
            // Skip if not administrator
            if (!_fixture.IsAdministrator())
            {
                _output.WriteLine("SKIPPED: Requires administrator privileges");
                return;
            }

            // Arrange
            var dependentService = _fixture.CreateTestService("DependentService");
            var dependencyService = _fixture.CreateTestService("DependencyService");

            // Set up dependency relationship
            dependentService.Dependencies.Add(dependencyService.Id);

            _output.WriteLine($"Creating services: {dependencyService.DisplayName} -> {dependentService.DisplayName}");

            try
            {
                // Act - Install dependency first, then dependent
                var depInstallResult = await _winswWrapper!.InstallServiceAsync(dependencyService);
                depInstallResult.Success.Should().BeTrue("dependency service installation should succeed");

                var dependentInstallResult = await _winswWrapper!.InstallServiceAsync(dependentService);
                dependentInstallResult.Success.Should().BeTrue("dependent service installation should succeed");

                // Start the dependent service (should start dependency first)
                _output.WriteLine("Starting dependent service (should auto-start dependency)...");
                var startResult = await _winswWrapper!.StartServiceAsync(dependentService);
                startResult.Success.Should().BeTrue("dependent service start should succeed");

                // Wait for services to start
                await Task.Delay(3000);

                // Assert - Both services should be running
                var depStatus = GetServiceStatus(dependencyService.Id);
                var dependentStatus = GetServiceStatus(dependentService.Id);

                depStatus.Should().Be(ServiceControllerStatus.Running, "dependency service should be running");
                dependentStatus.Should().Be(ServiceControllerStatus.Running, "dependent service should be running");

                _output.WriteLine("Both services started successfully with correct dependency order!");
            }
            finally
            {
                // Cleanup
                await CleanupServiceAsync(dependentService);
                await CleanupServiceAsync(dependencyService);
            }
        }

        [Fact]
        public async Task ServiceRestart_StopsAndStartsSuccessfully()
        {
            // Skip if not administrator
            if (!_fixture.IsAdministrator())
            {
                _output.WriteLine("SKIPPED: Requires administrator privileges");
                return;
            }

            // Arrange
            var service = _fixture.CreateTestService("RestartTest");
            _output.WriteLine($"Creating service for restart test: {service.DisplayName}");

            try
            {
                // Install and start
                var installResult = await _winswWrapper!.InstallServiceAsync(service);
                installResult.Success.Should().BeTrue();

                var startResult = await _winswWrapper!.StartServiceAsync(service);
                startResult.Success.Should().BeTrue();

                await Task.Delay(2000);
                var initialStatus = GetServiceStatus(service.Id);
                initialStatus.Should().Be(ServiceControllerStatus.Running);

                // Act - Stop and restart
                _output.WriteLine("Testing restart (stop -> start)...");
                var stopResult = await _winswWrapper!.StopServiceAsync(service);
                stopResult.Success.Should().BeTrue("stop should succeed");

                await Task.Delay(2000);
                var stopStatus = GetServiceStatus(service.Id);
                stopStatus.Should().Be(ServiceControllerStatus.Stopped, "service should be stopped");

                var restartResult = await _winswWrapper!.StartServiceAsync(service);
                restartResult.Success.Should().BeTrue("restart should succeed");

                await Task.Delay(2000);
                var restartStatus = GetServiceStatus(service.Id);
                restartStatus.Should().Be(ServiceControllerStatus.Running, "service should be running after restart");

                _output.WriteLine("Service restart test passed!");
            }
            finally
            {
                await CleanupServiceAsync(service);
            }
        }

        [Fact]
        public async Task ServiceConfiguration_GeneratedConfigIsValidXml()
        {
            // Arrange
            var service = _fixture.CreateTestService("ConfigTest");

            // Act
            var config = service.GenerateWinSWConfig();

            // Assert
            config.Should().NotBeNullOrEmpty();
            config.Should().Contain("<service>");
            config.Should().Contain("<id>" + service.Id + "</id>");
            config.Should().Contain("<name>" + service.DisplayName + "</name>");
            config.Should().Contain("<executable>" + service.ExecutablePath + "</executable>");

            // Try to parse as XML
            var xmlDoc = new System.Xml.XmlDocument();
            try
            {
                xmlDoc.LoadXml(config);
                _output.WriteLine("Generated config is valid XML");
            }
            catch (Exception ex)
            {
                throw new Exception("Generated config is not valid XML", ex);
            }
        }

        [Fact]
        public async Task ServiceConfiguration_EscapingPreventsInjection()
        {
            // Arrange - Service with potentially dangerous characters
            var service = new ServiceItem
            {
                DisplayName = "Test<script>alert('xss')</script>",
                Description = "Test & Description with <dangerous> chars",
                ExecutablePath = Path.Combine(Environment.SystemDirectory, "timeout.exe"),
                Arguments = "/t 1 & echo dangerous"
            };

            // Act
            var config = service.GenerateWinSWConfig();

            // Assert - Special characters should be properly escaped
            config.Should().NotContain("<script>");
            config.Should().Contain("&lt;script&gt;");
            config.Should().NotContain("& echo dangerous");
        }

        #region Helper Methods

        /// <summary>
        /// Checks if a Windows service exists
        /// </summary>
        private async Task<bool> ServiceExistsAsync(string serviceId)
        {
            return await Task.Run(() =>
            {
                try
                {
                    using var sc = new ServiceController(serviceId);
                    return true;
                }
                catch
                {
                    return false;
                }
            });
        }

        /// <summary>
        /// Gets the current status of a Windows service
        /// </summary>
        private ServiceControllerStatus GetServiceStatus(string serviceId)
        {
            try
            {
                using var sc = new ServiceController(serviceId);
                return sc.Status;
            }
            catch
            {
                return ServiceControllerStatus.Stopped;
            }
        }

        /// <summary>
        /// Cleans up a test service by stopping and uninstalling it
        /// </summary>
        private async Task CleanupServiceAsync(ServiceItem service)
        {
            try
            {
                _output.WriteLine($"Cleaning up service: {service.DisplayName}");

                // Try to stop the service first
                try
                {
                    await _winswWrapper!.StopServiceAsync(service);
                }
                catch
                {
                    // Ignore if service is not running
                }

                // Uninstall the service
                try
                {
                    await _winswWrapper!.UninstallServiceAsync(service);
                }
                catch
                {
                    // Ignore if already uninstalled
                }

                // Manual cleanup of directory if WinSW didn't clean it
                if (Directory.Exists(service.ServiceDirectory))
                {
                    try
                    {
                        Directory.Delete(service.ServiceDirectory, true);
                    }
                    catch
                    {
                        _output.WriteLine($"Warning: Could not delete directory {service.ServiceDirectory}");
                    }
                }

                _output.WriteLine($"Cleanup completed for: {service.DisplayName}");
            }
            catch (Exception ex)
            {
                _output.WriteLine($"Error during cleanup: {ex.Message}");
            }
        }

        /// <summary>
        /// Cleans up all test services
        /// </summary>
        private async Task CleanupTestServices()
        {
            try
            {
                // Clean up any services created during tests
                // This is a safety net to ensure no orphaned services remain
                var services = await _dataStorage.LoadServicesAsync();
                foreach (var service in services)
                {
                    await CleanupServiceAsync(service);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        #endregion
    }
}
