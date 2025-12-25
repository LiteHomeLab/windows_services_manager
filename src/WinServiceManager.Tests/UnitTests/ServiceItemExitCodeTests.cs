using Xunit;
using WinServiceManager.Models;

namespace WinServiceManager.Tests.UnitTests
{
    public class ServiceItemExitCodeTests
    {
        [Fact]
        public void GenerateWinSWConfig_WithEnableRestartOnExit_IncludesOnfailureElement()
        {
            // Arrange
            var service = new ServiceItem
            {
                Id = "test-service",
                DisplayName = "Test Service",
                ExecutablePath = @"C:\test\app.exe",
                WorkingDirectory = @"C:\test",
                EnableRestartOnExit = true,
                RestartExitCode = 99
            };

            // Act
            var config = service.GenerateWinSWConfig();

            // Assert
            Assert.Contains("<onfailure>", config);
            Assert.Contains("<restart />", config);
            Assert.Contains("wrapper.bat", config);
        }

        [Fact]
        public void GenerateWinSWConfig_WithoutEnableRestartOnExit_DoesNotIncludeOnfailureElement()
        {
            // Arrange
            var service = new ServiceItem
            {
                Id = "test-service",
                DisplayName = "Test Service",
                ExecutablePath = @"C:\test\app.exe",
                WorkingDirectory = @"C:\test",
                EnableRestartOnExit = false
            };

            // Act
            var config = service.GenerateWinSWConfig();

            // Assert
            Assert.DoesNotContain("<onfailure>", config);
            Assert.DoesNotContain("wrapper.bat", config);
            Assert.Contains("C:\\test\\app.exe", config);
        }

        [Fact]
        public void GenerateWinSWConfig_WithCustomExitCode_UsesCorrectExitCode()
        {
            // Arrange
            var service = new ServiceItem
            {
                Id = "test-service",
                DisplayName = "Test Service",
                ExecutablePath = @"C:\test\app.exe",
                WorkingDirectory = @"C:\test",
                EnableRestartOnExit = true,
                RestartExitCode = 42
            };

            // Act
            var config = service.GenerateWinSWConfig();

            // Assert
            Assert.Contains("wrapper.bat", config);
            // The wrapper script should receive 42 as the restart exit code
            Assert.Contains("\" 42\"", config);
        }

        [Fact]
        public void GenerateWinSWConfig_WithScriptPath_IncludesScriptInWrapperArguments()
        {
            // Arrange
            var service = new ServiceItem
            {
                Id = "test-service",
                DisplayName = "Test Service",
                ExecutablePath = @"C:\Python\python.exe",
                ScriptPath = @"C:\test\script.py",
                Arguments = "--verbose",
                WorkingDirectory = @"C:\test",
                EnableRestartOnExit = true,
                RestartExitCode = 99
            };

            // Act
            var config = service.GenerateWinSWConfig();

            // Assert
            Assert.Contains("<onfailure>", config);
            Assert.Contains("wrapper.bat", config);
            // The wrapper should receive the original executable as first argument
            Assert.Contains("C:\\Python\\python.exe", config);
            // The script path should be in arguments
            Assert.Contains("C:\\test\\script.py", config);
        }

        [Fact]
        public void EnableRestartOnExit_DefaultValue_IsFalse()
        {
            // Arrange & Act
            var service = new ServiceItem();

            // Assert
            Assert.False(service.EnableRestartOnExit);
        }

        [Fact]
        public void RestartExitCode_DefaultValue_Is99()
        {
            // Arrange & Act
            var service = new ServiceItem();

            // Assert
            Assert.Equal(99, service.RestartExitCode);
        }
    }
}
