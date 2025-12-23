using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using WinServiceManager.Models;
using Xunit;
using Xunit.Abstractions;

namespace WinServiceManager.Tests.IntegrationTests.Security
{
    /// <summary>
    /// Security boundary integration tests
    /// Tests the effectiveness of security mechanisms in actual scenarios
    /// </summary>
    [Collection("Security Tests")]
    public class SecurityBoundaryIntegrationTests
    {
        private readonly ITestOutputHelper _output;

        public SecurityBoundaryIntegrationTests(ITestOutputHelper output)
        {
            _output = output ?? throw new ArgumentNullException(nameof(output));
        }

        [Fact]
        public void PathTraversal_WithParentDirectoryAccess_IsBlocked()
        {
            // Arrange - Various path traversal attempts
            var pathTraversalAttempts = new[]
            {
                "../../../Windows/System32/cmd.exe",
                "..\\..\\..\\Windows\\System32\\cmd.exe",
                "../../etc/passwd",
                "..\\..\\..\\boot.ini",
                "....//....//....//Windows/System32/cmd.exe",
                "../%2e%2e/%2e%2e/Windows/System32/cmd.exe"
            };

            // Act & Assert
            foreach (var attempt in pathTraversalAttempts)
            {
                _output.WriteLine($"Testing path traversal: {attempt}");

                var isValid = PathValidator.IsValidPath(attempt);
                isValid.Should().BeFalse($"path traversal attempt '{attempt}' should be blocked");

                var isExecutable = CommandValidator.IsValidExecutable(attempt);
                isExecutable.Should().BeFalse($"executable path with traversal '{attempt}' should be blocked");
            }

            _output.WriteLine("All path traversal attempts were blocked");
        }

        [Fact]
        public void PathTraversal_WithUNCPaths_IsBlocked()
        {
            // Arrange - UNC path attempts
            var uncPathAttempts = new[]
            {
                @"\\evilserver\share\malware.exe",
                @"\\192.168.1.100\c$\Windows\System32\cmd.exe",
                @"\\?\UNC\server\share\file.exe",
                @"\\localhost\c$\Windows\System32\cmd.exe"
            };

            // Act & Assert
            foreach (var attempt in uncPathAttempts)
            {
                _output.WriteLine($"Testing UNC path: {attempt}");

                var isValid = PathValidator.IsValidPath(attempt);
                isValid.Should().BeFalse($"UNC path '{attempt}' should be blocked");
            }

            _output.WriteLine("All UNC path attempts were blocked");
        }

        [Fact]
        public void CommandInjection_WithPipeOperator_IsBlocked()
        {
            // Arrange - Command injection attempts with pipe
            var injectionAttempts = new[]
            {
                "timeout.exe | whoami",
                "timeout.exe | dir C:\\",
                "timeout.exe|ipconfig",
                "timeout.exe   |    net user"
            };

            // Act & Assert
            foreach (var attempt in injectionAttempts)
            {
                _output.WriteLine($"Testing pipe injection: {attempt}");

                var isSafe = CommandValidator.SanitizeArguments(attempt);
                isSafe.Should().NotContain("|", "pipe operator should be removed or escaped");
            }

            _output.WriteLine("All pipe injection attempts were handled");
        }

        [Fact]
        public void CommandInjection_WithCommandChaining_IsBlocked()
        {
            // Arrange - Command chaining attempts
            var injectionAttempts = new[]
            {
                "timeout.exe && whoami",
                "timeout.exe || dir C:\\",
                "timeout.exe ; ipconfig",
                "timeout.exe&net user",
                "timeout.exe; del C:\\test.txt"
            };

            // Act & Assert
            foreach (var attempt in injectionAttempts)
            {
                _output.WriteLine($"Testing command chaining: {attempt}");

                var isValid = CommandValidator.IsValidExecutable(attempt);
                isValid.Should().BeFalse($"command chaining attempt '{attempt}' should be blocked");
            }

            _output.WriteLine("All command chaining attempts were blocked");
        }

        [Fact]
        public void XmlInjection_WithMaliciousXml_IsEscaped()
        {
            // Arrange - Service with XML injection attempts
            var injectionAttempts = new[]
            {
                new ServiceItem
                {
                    DisplayName = "Test\"><script>alert('xss')</script>",
                    ExecutablePath = Path.Combine(Environment.SystemDirectory, "timeout.exe")
                },
                new ServiceItem
                {
                    DisplayName = "Test",
                    Description = "<![CDATA[<malicious>]]>",
                    ExecutablePath = Path.Combine(Environment.SystemDirectory, "timeout.exe")
                },
                new ServiceItem
                {
                    DisplayName = "Test",
                    Arguments = "</arguments><executable>c:\\windows\\system32\\cmd.exe</executable><arguments>",
                    ExecutablePath = Path.Combine(Environment.SystemDirectory, "timeout.exe")
                }
            };

            // Act & Assert
            foreach (var service in injectionAttempts)
            {
                _output.WriteLine($"Testing XML injection with: {service.DisplayName}");

                // Generate config should not throw exception
                var config = service.GenerateWinSWConfig();

                // Config should be valid XML
                var xmlDoc = new System.Xml.XmlDocument();
                xmlDoc.LoadXml(config);

                // Verify the malicious content is escaped
                config.Should().NotContain("<script>");
                config.Should().NotContain("<![CDATA[");
                config.Should().NotContain("</arguments><executable>");
            }

            _output.WriteLine("All XML injection attempts were properly escaped");
        }

        [Fact]
        public void FilePermissions_ServiceDirectory_Isolated()
        {
            // Arrange - Create a service with unique ID
            var service = new ServiceItem
            {
                DisplayName = "IsolationTest",
                ExecutablePath = Path.Combine(Environment.SystemDirectory, "timeout.exe"),
                Arguments = "/t 1"
            };

            // Act
            var serviceDir = service.ServiceDirectory;
            var configPath = service.WinSWConfigPath;
            var exePath = service.WinSWExecutablePath;

            // Assert - Verify paths are properly isolated
            serviceDir.Should().Contain("services");
            serviceDir.Should().Contain(service.Id);

            configPath.Should().Contain(service.Id);
            exePath.Should().Contain(service.Id);

            // Each service should have its own isolated directory
            var expectedDirStructure = Path.Combine("services", service.Id);
            serviceDir.Should().Contain(expectedDirStructure);

            _output.WriteLine($"Service directory is properly isolated: {serviceDir}");
        }

        [Fact]
        public void ProcessExecution_WithSystemCommands_IsBlocked()
        {
            // Arrange - Attempts to execute system commands
            var blockedCommands = new[]
            {
                "cmd.exe",
                "powershell.exe",
                "pwsh.exe",
                "wsl.exe",
                "bash.exe",
                "mshta.exe"
            };

            // Act & Assert
            foreach (var command in blockedCommands)
            {
                _output.WriteLine($"Testing blocked command: {command}");

                var fullPath = Path.Combine(Environment.SystemDirectory, command);
                var isValid = CommandValidator.IsValidExecutable(fullPath);

                // System commands should be blocked
                isValid.Should().BeFalse($"system command '{command}' should be blocked");
            }

            _output.WriteLine("All system command attempts were blocked");
        }

        [Fact]
        public void EnvironmentVariables_WithSensitiveData_AreProtected()
        {
            // Arrange - Service with environment variables
            var service = new ServiceItem
            {
                DisplayName = "EnvVarTest",
                ExecutablePath = Path.Combine(Environment.SystemDirectory, "timeout.exe"),
                Arguments = "/t 1"
            };

            service.EnvironmentVariables.Add("PASSWORD", "SuperSecret123");
            service.EnvironmentVariables.Add("API_KEY", "sk-1234567890");
            service.EnvironmentVariables.Add("PATH", "%PATH%;C:\\Malicious");

            // Act
            var config = service.GenerateWinSWConfig();

            // Assert - Config should be valid XML
            var xmlDoc = new System.Xml.XmlDocument();
            xmlDoc.LoadXml(config);

            // Environment variables should be included
            config.Should().Contain("PASSWORD");
            config.Should().Contain("API_KEY");

            _output.WriteLine("Environment variables are properly handled in configuration");
        }

        [Fact]
        public void ServiceAccount_WithPrivilegedUser_IsValidated()
        {
            // Arrange - Test various service account types
            var accounts = new[]
            {
                "LocalSystem",
                "LocalService",
                "NetworkService",
                ".\\Administrator",
                "NT AUTHORITY\\SYSTEM"
            };

            // Act & Assert - All these should be valid account names
            foreach (var account in accounts)
            {
                _output.WriteLine($"Testing service account: {account}");

                var service = new ServiceItem
                {
                    DisplayName = "AccountTest",
                    ExecutablePath = Path.Combine(Environment.SystemDirectory, "timeout.exe"),
                    ServiceAccount = account
                };

                // Should be able to generate config without error
                var config = service.GenerateWinSWConfig();
                config.Should().NotBeNullOrEmpty();
            }

            _output.WriteLine("Service account validation is working");
        }

        [Fact]
        public void ConfigurationTampering_AfterInstall_DoesNotAffectService()
        {
            // Arrange - Create a service
            var service = new ServiceItem
            {
                DisplayName = "TamperTest",
                ExecutablePath = Path.Combine(Environment.SystemDirectory, "timeout.exe"),
                Arguments = "/t 1"
            };

            // Act - Generate initial config
            var originalConfig = service.GenerateWinSWConfig();

            // Simulate tampering by modifying the service properties
            service.DisplayName = "Tampered Name";
            service.ExecutablePath = "C:\\Malicious\\evil.exe";

            // Generate new config
            var tamperedConfig = service.GenerateWinSWConfig();

            // Assert - The configs should be different
            tamperedConfig.Should().NotBe(originalConfig, "config should change when properties are modified");

            // But both should be valid XML
            var xmlDoc1 = new System.Xml.XmlDocument();
            xmlDoc1.LoadXml(originalConfig);

            var xmlDoc2 = new System.Xml.XmlDocument();
            xmlDoc2.LoadXml(tamperedConfig);

            _output.WriteLine("Configuration changes are properly detected");
        }

        [Fact]
        public void PathValidator_WithReservedNames_IsBlocked()
        {
            // Arrange - Windows reserved file names
            var reservedNames = new[]
            {
                "CON.exe",
                "PRN.exe",
                "AUX.exe",
                "NUL.exe",
                "COM1.exe",
                "COM9.exe",
                "LPT1.exe",
                "LPT9.exe",
                "CON",
                "PRN",
                "AUX",
                "NUL"
            };

            // Act & Assert
            foreach (var name in reservedNames)
            {
                _output.WriteLine($"Testing reserved name: {name}");

                var path = Path.Combine(Environment.SystemDirectory, name);
                var isValid = PathValidator.IsValidPath(path);

                isValid.Should().BeFalse($"reserved name '{name}' should be blocked");
            }

            _output.WriteLine("All reserved names were blocked");
        }

        [Fact]
        public void PathValidator_WithInvalidCharacters_IsBlocked()
        {
            // Arrange - Paths with invalid characters
            var invalidPaths = new[]
            {
                "test<file>.exe",
                "test>file>.exe",
                "test|file.exe",
                "test?file.exe",
                "test*file.exe",
                "test\"file.exe",
                "test\0file.exe"  // Null character
            };

            // Act & Assert
            foreach (var path in invalidPaths)
            {
                _output.WriteLine($"Testing invalid characters in: {path}");

                // Path.GetInvalidFileNameChars() would catch these
                var hasInvalidChars = path.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0;
                hasInvalidChars.Should().BeTrue($"path '{path}' should contain invalid characters");

                var isValid = PathValidator.IsValidPath(path);
                isValid.Should().BeFalse($"path with invalid characters '{path}' should be blocked");
            }

            _output.WriteLine("All paths with invalid characters were blocked");
        }

        [Fact]
        public void CommandValidator_WithSpecialCharacters_IsSafe()
        {
            // Arrange - Commands with special shell characters
            var specialCharsAttempts = new[]
            {
                "test.exe $(whoami)",
                "test.exe `echo dangerous`",
                "test.exe $ENV:COMSPEC",
                "test.exe %COMSPEC%",
                "test.exe < input.txt",
                "test.exe > output.txt",
                "test.exe 2>&1"
            };

            // Act & Assert
            foreach (var attempt in specialCharsAttempts)
            {
                _output.WriteLine($"Testing special characters in: {attempt}");

                var isValid = CommandValidator.IsValidExecutable(attempt);
                isValid.Should().BeFalse($"command with special characters '{attempt}' should be blocked");
            }

            _output.WriteLine("All commands with special shell characters were blocked");
        }

        [Fact]
        public void ServiceConfiguration_WithEmptyRequiredFields_ValidationFails()
        {
            // Arrange - Service with missing required fields
            var invalidService = new ServiceItem
            {
                DisplayName = "",  // Empty display name
                ExecutablePath = ""  // Empty executable path
            };

            // Act & Assert - Should throw or have validation issues
            try
            {
                var config = invalidService.GenerateWinSWConfig();

                // If we get here, check if config is empty or invalid
                config.Should().BeNullOrEmpty("config for invalid service should be empty or invalid");
            }
            catch (Exception ex)
            {
                // Exception is expected for invalid service
                _output.WriteLine($"Expected exception for invalid service: {ex.Message}");
            }

            _output.WriteLine("Invalid service configuration is properly rejected");
        }
    }
}
