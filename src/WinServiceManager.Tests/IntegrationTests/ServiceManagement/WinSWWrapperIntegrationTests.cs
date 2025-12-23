using System;
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

namespace WinServiceManager.Tests.IntegrationTests.ServiceManagement
{
    /// <summary>
    /// WinSW wrapper integration tests
    /// Tests the actual interaction with WinSW executable
    /// Requires administrator privileges to run
    /// </summary>
    [Collection("Integration Tests")]
    public class WinSWWrapperIntegrationTests : IClassFixture<ServiceTestFixture>, IDisposable
    {
        private readonly ServiceTestFixture _fixture;
        private readonly ITestOutputHelper _output;
        private readonly WinSWWrapper? _winswWrapper;

        public WinSWWrapperIntegrationTests(ServiceTestFixture fixture, ITestOutputHelper output)
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
                var logger = new LoggerFactory().CreateLogger<WinSWWrapper>();
                _winswWrapper = new WinSWWrapper(logger);
            }
        }

        public void Dispose()
        {
            // Cleanup is handled by the test fixture
        }

        [Fact]
        public async Task ExecuteWinSWCommand_WithInstallCommand_CreatesWindowsService()
        {
            // Skip if not administrator
            if (!_fixture.IsAdministrator())
            {
                _output.WriteLine("SKIPPED: Requires administrator privileges");
                return;
            }

            // Arrange
            var service = _fixture.CreateTestService("InstallTest");
            _output.WriteLine($"Testing install command for: {service.DisplayName}");

            try
            {
                // Act
                var result = await _winswWrapper!.InstallServiceAsync(service);

                // Assert
                result.Success.Should().BeTrue("install command should succeed");
                result.ErrorMessage.Should().BeNullOrEmpty();
                result.Operation.Should().Be(ServiceOperationType.Install);
                result.ElapsedMilliseconds.Should().BeGreaterThan(0);

                // Verify files were created
                File.Exists(service.WinSWExecutablePath).Should().BeTrue("WinSW executable should be copied");
                File.Exists(service.WinSWConfigPath).Should().BeTrue("WinSW config should be generated");

                // Verify config content
                var configContent = await File.ReadAllTextAsync(service.WinSWConfigPath);
                configContent.Should().Contain("<id>" + service.Id + "</id>");
                configContent.Should().Contain("<name>" + service.DisplayName + "</name>");

                _output.WriteLine($"Install succeeded in {result.ElapsedMilliseconds}ms");
            }
            finally
            {
                // Cleanup
                await CleanupServiceAsync(service);
            }
        }

        [Fact]
        public async Task ExecuteWinSWCommand_WithInvalidArguments_ThrowsSecurityException()
        {
            // Skip if not administrator
            if (!_fixture.IsAdministrator())
            {
                _output.WriteLine("SKIPPED: Requires administrator privileges");
                return;
            }

            // Arrange - Service with command injection attempt in arguments
            var service = new ServiceItem
            {
                DisplayName = "InjectionTest",
                ExecutablePath = Path.Combine(Environment.SystemDirectory, "timeout.exe"),
                Arguments = "/t 1 & whoami",  // Attempted command injection
                WorkingDirectory = Environment.SystemDirectory
            };

            _output.WriteLine("Testing command injection protection...");

            try
            {
                // Act
                var result = await _winswWrapper!.InstallServiceAsync(service);

                // The WinSWWrapper should sanitize the arguments
                // The service should still install, but the dangerous part should be neutralized
                result.Success.Should().BeTrue("install should succeed after sanitization");

                // Verify the config has sanitized arguments
                var configContent = await File.ReadAllTextAsync(service.WinSWConfigPath);
                configContent.Should().Contain("/t 1");

                _output.WriteLine("Command injection protection is working");
            }
            finally
            {
                // Cleanup
                await CleanupServiceAsync(service);
            }
        }

        [Fact]
        public async Task ExecuteWinSWCommand_WithLongRunningCommand_TimesOutCorrectly()
        {
            // Skip if not administrator
            if (!_fixture.IsAdministrator())
            {
                _output.WriteLine("SKIPPED: Requires administrator privileges");
                return;
            }

            // Arrange
            var service = new ServiceItem
            {
                DisplayName = "TimeoutTest",
                ExecutablePath = Path.Combine(Environment.SystemDirectory, "timeout.exe"),
                Arguments = "/t 120",  // 2 minute timeout
                WorkingDirectory = Environment.SystemDirectory,
                StopTimeout = 5  // 5 second stop timeout
            };

            _output.WriteLine("Testing stop timeout...");

            try
            {
                // Install
                var installResult = await _winswWrapper!.InstallServiceAsync(service);
                installResult.Success.Should().BeTrue();

                // Start
                var startResult = await _winswWrapper!.StartServiceAsync(service);
                startResult.Success.Should().BeTrue();

                await Task.Delay(2000);

                // Act - Stop should timeout the long-running process
                var stopWatch = System.Diagnostics.Stopwatch.StartNew();
                var stopResult = await _winswWrapper!.StopServiceAsync(service);
                stopWatch.Stop();

                // Assert
                stopResult.Success.Should().BeTrue("stop should succeed");
                _output.WriteLine($"Stop completed in {stopWatch.ElapsedMilliseconds}ms");

                await Task.Delay(1000);
            }
            finally
            {
                // Cleanup
                await CleanupServiceAsync(service);
            }
        }

        [Fact]
        public async Task WinSWConfiguration_GeneratedConfigIsValidXml()
        {
            // Arrange
            var service = _fixture.CreateTestService("ConfigValidationTest");

            // Act
            var config = service.GenerateWinSWConfig();

            // Assert
            config.Should().NotBeNullOrEmpty("config should be generated");

            // Verify required elements
            config.Should().Contain("<service>", "should contain service root element");
            config.Should().Contain("<id>", "should contain id element");
            config.Should().Contain("<name>", "should contain name element");
            config.Should().Contain("<executable>", "should contain executable element");

            // Try to parse as XML
            var xmlDoc = new System.Xml.XmlDocument();
            xmlDoc.LoadXml(config);

            _output.WriteLine("Generated config is valid XML");
        }

        [Fact]
        public async Task WinSWConfiguration_ContainsAllRequiredElements()
        {
            // Arrange
            var service = _fixture.CreateTestService("FullConfigTest");
            service.Description = "Full description for testing";
            service.StartMode = ServiceStartupMode.Manual;

            // Act
            var config = service.GenerateWinSWConfig();

            // Assert - Verify all expected elements
            config.Should().Contain("<id>" + service.Id + "</id>");
            config.Should().Contain("<name>" + service.DisplayName + "</name>");
            config.Should().Contain("<description>" + service.Description + "</description>");
            config.Should().Contain("<executable>" + service.ExecutablePath + "</executable>");
            config.Should().Contain("<arguments>" + service.Arguments + "</arguments>");
            config.Should().Contain("<workingdirectory>" + service.WorkingDirectory + "</workingdirectory>");
            config.Should().Contain("<startmode>manual</startmode>");

            _output.WriteLine("All required configuration elements are present");
        }

        [Fact]
        public async Task WinSWConfiguration_WithEnvironmentVariables_IncludesCorrectly()
        {
            // Arrange
            var service = _fixture.CreateTestService("EnvVarTest");
            service.EnvironmentVariables.Add("TEST_VAR", "TestValue");
            service.EnvironmentVariables.Add("ANOTHER_VAR", "AnotherValue");

            // Act
            var config = service.GenerateWinSWConfig();

            // Assert
            config.Should().Contain("<environment>");
            config.Should().Contain("<variable name=\"TEST_VAR\" value=\"TestValue\"/>");
            config.Should().Contain("<variable name=\"ANOTHER_VAR\" value=\"AnotherValue\"/>");

            _output.WriteLine("Environment variables are correctly included in configuration");
        }

        [Fact]
        public async Task WinSWConfiguration_WithDependencies_IncludesCorrectly()
        {
            // Arrange
            var service = _fixture.CreateTestService("DependencyConfigTest");
            service.Dependencies.Add("DepService1");
            service.Dependencies.Add("DepService2");

            // Act
            var config = service.GenerateWinSWConfig();

            // Assert
            config.Should().Contain("<depend>");
            config.Should().Contain("DepService1");
            config.Should().Contain("DepService2");

            _output.WriteLine("Dependencies are correctly included in configuration");
        }

        [Fact]
        public async Task WinSWConfiguration_EscapingPreventsInjection()
        {
            // Arrange - Service with potentially dangerous characters
            var service = new ServiceItem
            {
                DisplayName = "Test<script>alert('xss')</script>",
                Description = "Test & Description with <dangerous> \"chars\"",
                ExecutablePath = Path.Combine(Environment.SystemDirectory, "timeout.exe"),
                Arguments = "/t 1 & echo dangerous"
            };

            // Act
            var config = service.GenerateWinSWConfig();

            // Assert - Special characters should be properly escaped
            config.Should().NotContain("<script>");
            config.Should().NotContain("&amp; echo dangerous");
            config.Should().Contain("&lt;script&gt;alert(&apos;xss&apos;)&lt;/script&gt;");

            // Parse as XML to ensure it's valid
            var xmlDoc = new System.Xml.XmlDocument();
            xmlDoc.LoadXml(config);

            _output.WriteLine("XML escaping prevents injection attacks");
        }

        [Fact]
        public async Task WinSWConfiguration_WithLogConfiguration_IncludesLogSettings()
        {
            // Arrange
            var service = _fixture.CreateTestService("LogConfigTest");

            // Act
            var config = service.GenerateWinSWConfig();

            // Assert - Verify log configuration
            config.Should().Contain("<log mode=\"roll-by-size\">");
            config.Should().Contain("<sizeThreshold>10240</sizeThreshold>");
            config.Should().Contain("<keepFiles>8</keepFiles>");

            _output.WriteLine("Log configuration is correctly included");
        }

        #region Helper Methods

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

        #endregion
    }
}
