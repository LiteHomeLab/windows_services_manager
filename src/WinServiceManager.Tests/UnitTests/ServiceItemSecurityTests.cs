using System;
using System.IO;
using System.Xml;
using WinServiceManager.Models;
using Xunit;

namespace WinServiceManager.Tests.UnitTests
{
    /// <summary>
    /// Unit tests for ServiceItem security features
    /// Tests property validation and XML generation security
    /// </summary>
    public class ServiceItemSecurityTests
    {
        #region ExecutablePath Property Tests

        [Fact]
        public void ExecutablePath_ValidPath_SetsValue()
        {
            // Arrange
            var serviceItem = new ServiceItem();
            string validPath = Path.Combine(Path.GetTempPath(), "app.exe");

            // Act
            serviceItem.ExecutablePath = validPath;

            // Assert
            Assert.Equal(validPath, serviceItem.ExecutablePath);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void ExecutablePath_NullOrEmpty_SetsValue(string path)
        {
            // Arrange
            var serviceItem = new ServiceItem();

            // Act
            serviceItem.ExecutablePath = path;

            // Assert
            Assert.Equal(path, serviceItem.ExecutablePath);
        }

        [Theory]
        [InlineData("../../../Windows/System32/cmd.exe")]
        [InlineData("..\\..\\..\\Windows\\System32\\powershell.exe")]
        [InlineData(@"\\server\share\malware.exe")]
        [InlineData("C:\\Path\\With<Invalid>Chars.exe")]
        public void ExecutablePath_InvalidPath_ThrowsArgumentException(string path)
        {
            // Arrange
            var serviceItem = new ServiceItem();

            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => serviceItem.ExecutablePath = path);
            Assert.Contains("Invalid executable path", exception.Message);
        }

        #endregion

        #region ScriptPath Property Tests

        [Fact]
        public void ScriptPath_ValidPath_SetsValue()
        {
            // Arrange
            var serviceItem = new ServiceItem();
            string validPath = Path.Combine(Path.GetTempPath(), "script.py");

            // Act
            serviceItem.ScriptPath = validPath;

            // Assert
            Assert.Equal(validPath, serviceItem.ScriptPath);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void ScriptPath_NullOrEmpty_SetsValue(string path)
        {
            // Arrange
            var serviceItem = new ServiceItem();

            // Act
            serviceItem.ScriptPath = path;

            // Assert
            Assert.Equal(path, serviceItem.ScriptPath);
        }

        [Theory]
        [InlineData("../../../config.ini")]
        [InlineData("..\\..\\..\\secret.txt")]
        [InlineData(@"\\server\share\\script.py")]
        [InlineData("C:\\Path\\With|Pipe.py")]
        public void ScriptPath_InvalidPath_ThrowsArgumentException(string path)
        {
            // Arrange
            var serviceItem = new ServiceItem();

            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => serviceItem.ScriptPath = path);
            Assert.Contains("Invalid script path", exception.Message);
        }

        #endregion

        #region WorkingDirectory Property Tests

        [Fact]
        public void WorkingDirectory_ValidPath_SetsValue()
        {
            // Arrange
            var serviceItem = new ServiceItem();
            string validPath = Path.GetTempPath();

            // Act
            serviceItem.WorkingDirectory = validPath;

            // Assert
            Assert.Equal(validPath, serviceItem.WorkingDirectory);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void WorkingDirectory_NullOrEmpty_SetsValue(string path)
        {
            // Arrange
            var serviceItem = new ServiceItem();

            // Act
            serviceItem.WorkingDirectory = path;

            // Assert
            Assert.Equal(path, serviceItem.WorkingDirectory);
        }

        [Theory]
        [InlineData("../../../Windows/System32")]
        [InlineData("..\\..\\..\\Program Files")]
        [InlineData(@"\\server\share")]
        [InlineData("C:\\Path\\With?Question")]
        public void WorkingDirectory_InvalidPath_ThrowsArgumentException(string path)
        {
            // Arrange
            var serviceItem = new ServiceItem();

            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => serviceItem.WorkingDirectory = path);
            Assert.Contains("Invalid working directory", exception.Message);
        }

        #endregion

        #region GetFullArguments Tests

        [Fact]
        public void GetFullArguments_NoScriptPath_ReturnsArguments()
        {
            // Arrange
            var serviceItem = new ServiceItem
            {
                ScriptPath = null,
                Arguments = "--port 8080 --debug"
            };

            // Act
            string result = serviceItem.GetFullArguments();

            // Assert
            Assert.Equal("--port 8080 --debug", result);
        }

        [Fact]
        public void GetFullArguments_WithScriptPath_ReturnsQuotedScriptAndArguments()
        {
            // Arrange
            var serviceItem = new ServiceItem
            {
                ScriptPath = @"C:\Scripts\app.py",
                Arguments = "--port 8080 --debug"
            };

            // Act
            string result = serviceItem.GetFullArguments();

            // Assert
            Assert.Equal("\"C:\\Scripts\\app.py\" --port 8080 --debug", result);
        }

        [Fact]
        public void GetFullArguments_EmptyArguments_ReturnsQuotedScriptOnly()
        {
            // Arrange
            var serviceItem = new ServiceItem
            {
                ScriptPath = @"C:\Scripts\run.bat",
                Arguments = ""
            };

            // Act
            string result = serviceItem.GetFullArguments();

            // Assert
            Assert.Equal("\"C:\\Scripts\\run.bat\" ", result);
        }

        #endregion

        #region GenerateWinSWConfig Tests

        [Fact]
        public void GenerateWinSWConfig_ValidData_ReturnsValidXml()
        {
            // Arrange
            var serviceItem = new ServiceItem
            {
                DisplayName = "Test Service",
                Description = "A test service",
                ExecutablePath = @"C:\App\app.exe",
                Arguments = "--port 8080",
                WorkingDirectory = @"C:\App"
            };

            // Act
            string result = serviceItem.GenerateWinSWConfig();

            // Assert
            Assert.NotNull(result);

            // Parse as XML to ensure it's valid
            var xmlDoc = new XmlDocument();
            Assert.DoesNotThrow(() => xmlDoc.LoadXml(result));

            // Verify XML contains expected elements
            Assert.Contains("<service>", result);
            Assert.Contains("<id>", result);
            Assert.Contains("<name>Test Service</name>", result);
            Assert.Contains("<description>A test service</description>", result);
            Assert.Contains("<executable>C:\\App\\app.exe</executable>", result);
            Assert.Contains("<arguments>--port 8080</arguments>", result);
            Assert.Contains("<workingdirectory>C:\\App</workingdirectory>", result);
        }

        [Theory]
        [InlineData("Service & Name", "Service &amp; Name")]
        [InlineData("Service <Test>", "Service &lt;Test&gt;")]
        [InlineData("Service \"Quotes\"", "Service &quot;Quotes&quot;")]
        [InlineData("Service 'Apostrophe'", "Service 'Apostrophe'")]
        [InlineData("Service \0WithNull", "Service &#0;WithNull")]
        public void GenerateWinSWConfig_DisplayNameWithSpecialChars_EscapesCorrectly(string displayName, string expectedEscaped)
        {
            // Arrange
            var serviceItem = new ServiceItem
            {
                DisplayName = displayName,
                ExecutablePath = @"C:\App\app.exe",
                WorkingDirectory = @"C:\App"
            };

            // Act
            string result = serviceItem.GenerateWinSWConfig();

            // Assert
            Assert.Contains(expectedEscaped, result);
        }

        [Theory]
        [InlineData("Description with & symbols", "Description with &amp; symbols")]
        [InlineData("Description <with> tags", "Description &lt;with&gt; tags")]
        [InlineData("Description \"with quotes\"", "Description &quot;with quotes&quot;")]
        public void GenerateWinSWConfig_DescriptionWithSpecialChars_EscapesCorrectly(string description, string expectedEscaped)
        {
            // Arrange
            var serviceItem = new ServiceItem
            {
                Description = description,
                ExecutablePath = @"C:\App\app.exe",
                WorkingDirectory = @"C:\App"
            };

            // Act
            string result = serviceItem.GenerateWinSWConfig();

            // Assert
            Assert.Contains(expectedEscaped, result);
        }

        [Theory]
        [InlineData("C:\\Path\\With<Invalid>Chars.exe")]
        [InlineData("C:\\Path\\With|Pipe.exe")]
        [InlineData("C:\\Path\\With&And.exe")]
        public void GenerateWinSWConfig_ExecutablePathWithSpecialChars_EscapesCorrectly(string executablePath)
        {
            // Arrange
            var serviceItem = new ServiceItem
            {
                ExecutablePath = executablePath,
                WorkingDirectory = @"C:\App"
            };

            // Act
            string result = serviceItem.GenerateWinSWConfig();

            // Assert
            // The executable path should be XML-escaped in the output
            Assert.DoesNotContain("<invalid>", result, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void GenerateWinSWConfig_ArgumentsWithScriptPath_IncludesQuotedScript()
        {
            // Arrange
            var serviceItem = new ServiceItem
            {
                ExecutablePath = @"C:\Python\python.exe",
                ScriptPath = @"C:\Scripts\app.py",
                Arguments = "--port 8080",
                WorkingDirectory = @"C:\App"
            };

            // Act
            string result = serviceItem.GenerateWinSWConfig();

            // Assert
            Assert.Contains("&quot;C:\\Scripts\\app.py&quot; --port 8080", result);
        }

        [Fact]
        public void GenerateWinSWConfig_WorkingDirectoryWithSpecialChars_EscapesCorrectly()
        {
            // Arrange
            var serviceItem = new ServiceItem
            {
                ExecutablePath = @"C:\App\app.exe",
                WorkingDirectory = @"C:\Path & Name\With<Spaces>"
            };

            // Act
            string result = serviceItem.GenerateWinSWConfig();

            // Assert
            Assert.Contains("C:\\Path &amp; Name\\With&lt;Spaces&gt;", result);
        }

        [Fact]
        public void GenerateWinSWConfig_AllPropertiesSet_GeneratesCompleteConfig()
        {
            // Arrange
            var serviceItem = new ServiceItem
            {
                DisplayName = "Complete Test Service",
                Description = "A complete test service with all properties",
                ExecutablePath = @"C:\App\app.exe",
                ScriptPath = @"C:\Scripts\run.py",
                Arguments = "--port 8080 --debug",
                WorkingDirectory = @"C:\App"
            };

            // Act
            string result = serviceItem.GenerateWinSWConfig();

            // Assert
            // Verify all elements are present and properly escaped
            Assert.Contains("<?xml version=\"1.0\" encoding=\"utf-8\"?>", result);
            Assert.Contains("<service>", result);
            Assert.Contains($"<id>{serviceItem.Id}</id>", result);
            Assert.Contains("<name>Complete Test Service</name>", result);
            Assert.Contains("<description>A complete test service with all properties</description>", result);
            Assert.Contains("<executable>C:\\App\\app.exe</executable>", result);
            Assert.Contains("&quot;C:\\Scripts\\run.py&quot; --port 8080 --debug", result);
            Assert.Contains("<workingdirectory>C:\\App</workingdirectory>", result);
            Assert.Contains("<log mode=\"roll-by-size\">", result);
            Assert.Contains("<sizeThreshold>10240</sizeThreshold>", result);
            Assert.Contains("<keepFiles>8</keepFiles>", result);
            Assert.Contains("<stopparentprocessfirst>true</stopparentprocessfirst>", result);
            Assert.Contains("</service>", result);
        }

        #endregion
    }
}