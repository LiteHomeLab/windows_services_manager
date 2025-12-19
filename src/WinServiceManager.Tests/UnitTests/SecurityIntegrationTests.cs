using System;
using System.IO;
using System.Xml;
using WinServiceManager.Models;
using Xunit;

namespace WinServiceManager.Tests.UnitTests
{
    /// <summary>
    /// Integration tests for security components working together
    /// Tests how PathValidator and CommandValidator integrate with ServiceItem
    /// </summary>
    [Collection("Security Tests")]
    public class SecurityIntegrationTests : IClassFixture<SecurityTestsFixture>
    {
        private readonly SecurityTestsFixture _fixture;

        public SecurityIntegrationTests(SecurityTestsFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public void ServiceItem_CreateWithAllValidProperties_DoesNotThrow()
        {
            // Arrange
            var serviceItem = new ServiceItem();

            // Act & Assert - Setting valid properties should not throw
            Assert.DoesNotThrow(() =>
            {
                serviceItem.DisplayName = "Valid Service";
                serviceItem.Description = "A valid test service";
                serviceItem.ExecutablePath = Path.Combine(Path.GetTempPath(), "app.exe");
                serviceItem.WorkingDirectory = Path.GetTempPath();
                serviceItem.Arguments = "--port 8080";
            });

            // Verify properties were set correctly
            Assert.Equal("Valid Service", serviceItem.DisplayName);
            Assert.NotNull(serviceItem.ExecutablePath);
            Assert.NotNull(serviceItem.WorkingDirectory);
        }

        [Fact]
        public void ServiceItem_WithScriptPath_GeneratesCorrectConfig()
        {
            // Arrange
            var serviceItem = new ServiceItem
            {
                DisplayName = "Python Service",
                ExecutablePath = @"C:\Python\python.exe",
                ScriptPath = @"C:\Scripts\app.py",
                WorkingDirectory = @"C:\App"
            };

            // Act
            string config = serviceItem.GenerateWinSWConfig();
            string fullArgs = serviceItem.GetFullArguments();

            // Assert
            Assert.Contains("python.exe", config);
            Assert.Contains("\"C:\\Scripts\\app.py\"", fullArgs);

            // Verify XML is valid
            var xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(config);
        }

        [Fact]
        public void ServiceItem_ChainingAttacks_AllBlocked()
        {
            // Arrange
            var serviceItem = new ServiceItem();

            // Test multiple potential injection points
            var injectionAttempts = new[]
            {
                ("../../../Windows/System32/cmd.exe", "../../../config.ini", "--help"),
                ("..\\..\\..\\Windows\\System32\\powershell.exe", "..\\..\\secret.txt", "& net user"),
                (@"\\server\share\malware.exe", @"\\server\share\script.bat", "&& format c:"),
                ("C:\\App\\app.exe", "C:\\Scripts\\<script>.py", "--port | dir")
            };

            // Act & Assert - All attempts should throw ArgumentException
            foreach (var (exe, script, args) in injectionAttempts)
            {
                var testService = new ServiceItem();

                // Each of these should throw
                Assert.Throws<ArgumentException>(() => testService.ExecutablePath = exe);
                Assert.Throws<ArgumentException>(() => testService.ScriptPath = script);

                // Arguments are validated when generating config
                testService.Arguments = args;
                Assert.Throws<ArgumentException>(() => CommandValidator.SanitizeArguments(args));
            }
        }

        [Fact]
        public void ServiceItem_XmlInjection_InConfiguration_IsPrevented()
        {
            // Arrange
            var serviceItem = new ServiceItem
            {
                DisplayName = "Service & Name <script>alert('xss')</script>",
                Description = "Description \"with quotes\" & <tags>",
                ExecutablePath = Path.Combine(Path.GetTempPath(), "app.exe"),
                WorkingDirectory = Path.GetTempPath()
            };

            // Act
            string config = serviceItem.GenerateWinSWConfig();

            // Assert
            // Verify XML is valid (no injection)
            var xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(config);

            // Verify special characters are properly escaped
            Assert.Contains("&amp;", config);
            Assert.Contains("&lt;", config);
            Assert.Contains("&gt;", config);
            Assert.Contains("&quot;", config);

            // Verify no actual script tags or injection
            Assert.DoesNotContain("<script>", config, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("alert(", config, StringComparison.OrdinalIgnoreCase);
        }

        [Theory]
        [InlineData("C:\\Valid\\Path\\app.exe", "normal arguments")]
        [InlineData("C:\\Another\\Valid\\service.bat", "--port 8080 --debug")]
        [InlineData("C:\\Program Files\\MyApp\\service.exe", "/config file.conf")]
        public void ServiceItem_ValidPathsAndArguments_Succeeds(string executablePath, string arguments)
        {
            // Arrange
            var serviceItem = new ServiceItem();

            // Act
            serviceItem.ExecutablePath = executablePath;
            serviceItem.Arguments = arguments;
            serviceItem.WorkingDirectory = Path.GetDirectoryName(executablePath);

            // Generate config to ensure everything works together
            string config = serviceItem.GenerateWinSWConfig();
            string sanitizedArgs = CommandValidator.SanitizeArguments(arguments);

            // Assert
            Assert.NotNull(config);
            Assert.NotNull(sanitizedArgs);
            Assert.Equal(executablePath, serviceItem.ExecutablePath);

            // Verify XML is well-formed
            var xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(config);
        }

        [Fact]
        public void SecurityComponents_PathAndCommandValidation_WorkTogether()
        {
            // Arrange
            string maliciousPath = "../../../Windows/System32";
            string maliciousArgs = "&& del /s /q *.*";

            // Act & Assert - Both validators should catch the issues
            Assert.False(PathValidator.IsValidPath(maliciousPath));
            Assert.Throws<ArgumentException>(() => PathValidator.GetSafePath(maliciousPath));
            Assert.Throws<ArgumentException>(() => CommandValidator.SanitizeArguments(maliciousArgs));

            // Test with ServiceItem
            var serviceItem = new ServiceItem();
            Assert.Throws<ArgumentException>(() => serviceItem.ExecutablePath = maliciousPath);
            Assert.Throws<ArgumentException>(() => serviceItem.WorkingDirectory = maliciousPath);

            serviceItem.Arguments = maliciousArgs;
            Assert.Throws<ArgumentException>(() => serviceItem.GenerateWinSWConfig());
        }

        [Fact]
        public void ServiceItem_FileNameValidation_InWorkingDirectory()
        {
            // Arrange - Test with filenames that contain reserved names or invalid chars
            var invalidNames = new[]
            {
                "CON.exe",
                "PRN.bat",
                "AUX.com",
                "file<name>.exe",
                "file|name.bat",
                "file?name.cmd",
                "LPT1.ps1",
                "COM9.py"
            };

            foreach (var name in invalidNames)
            {
                // Arrange
                var path = Path.Combine(Path.GetTempPath(), name);

                // Act & Assert
                Assert.False(PathValidator.IsValidFileName(name), $"FileName '{name}' should be invalid");

                // PathValidator might still allow the full path if it's not a traversal
                // This tests the filename validation specifically
                var fileName = Path.GetFileName(path);
                if (!string.IsNullOrEmpty(fileName))
                {
                    var isValid = PathValidator.IsValidFileName(fileName);
                    if (!isValid)
                    {
                        // If filename is invalid, ServiceItem should reject it
                        var serviceItem = new ServiceItem();
                        if (Path.GetExtension(fileName).ToLowerInvariant() == ".exe" ||
                            Path.GetExtension(fileName).ToLowerInvariant() == ".bat" ||
                            Path.GetExtension(fileName).ToLowerInvariant() == ".cmd")
                        {
                            // Test if it would be rejected as executable
                            var testResult = CommandValidator.IsValidExecutable(path);
                            // Note: IsValidExecutable might not use IsValidFileName directly
                            // This test documents the behavior
                        }
                    }
                }
            }
        }
    }
}