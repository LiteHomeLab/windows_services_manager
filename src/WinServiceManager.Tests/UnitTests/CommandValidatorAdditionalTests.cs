using System;
using WinServiceManager.Models;
using Xunit;

namespace WinServiceManager.Tests.UnitTests
{
    /// <summary>
    /// Additional unit tests for CommandValidator class
    /// Tests methods not covered in the original test file
    /// </summary>
    public class CommandValidatorAdditionalTests
    {
        #region IsValidInput Tests

        [Theory]
        [InlineData("--port 8080")]
        [InlineData("--version")]
        [InlineData("normal argument")]
        [InlineData("12345")]
        [InlineData("config.json")]
        [InlineData("--help")]
        [InlineData("-v")]
        [InlineData("-f filename.txt")]
        [InlineData("--timeout 30")]
        public void IsValidInput_ValidArguments_ReturnsTrue(string input)
        {
            // Act
            bool result = CommandValidator.IsValidInput(input);

            // Assert
            Assert.True(result);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("\t")]
        [InlineData(null)]
        public void IsValidInput_NullOrEmptyInput_ReturnsFalse(string input)
        {
            // Act
            bool result = CommandValidator.IsValidInput(input);

            // Assert
            Assert.False(result);
        }

        [Theory]
        [InlineData("arg && del")]
        [InlineData("arg || format")]
        [InlineData("arg; shutdown")]
        [InlineData("arg | more")]
        [InlineData("arg > file.txt")]
        [InlineData("arg < input.txt")]
        [InlineData("arg >> output.log")]
        public void IsValidInput_ContainsDangerousPatterns_ReturnsFalse(string input)
        {
            // Act
            bool result = CommandValidator.IsValidInput(input);

            // Assert
            Assert.False(result);
        }

        [Theory]
        [InlineData("cmd.exe")]
        [InlineData("powershell.exe")]
        [InlineData("net user")]
        [InlineData("del /s")]
        [InlineData("format c:")]
        [InlineData("reg delete")]
        [InlineData("start malware.exe")]
        [InlineData("taskkill /f")]
        [InlineData("powershell -c")]
        [InlineData("cmd /c")]
        public void IsValidInput_CommandInjectionPatterns_ReturnsFalse(string input)
        {
            // Act
            bool result = CommandValidator.IsValidInput(input);

            // Assert
            Assert.False(result);
        }

        [Theory]
        [InlineData("path with spaces/file.txt")]
        [InlineData("normal_argument")]
        [InlineData("config-file.json")]
        [InlineData("file_name.txt")]
        [InlineData("12345")]
        [InlineData("true")]
        [InlineData("false")]
        [InlineData("test-value")]
        public void IsValidInput_SafeCharacters_ReturnsTrue(string input)
        {
            // Act
            bool result = CommandValidator.IsValidInput(input);

            // Assert
            Assert.True(result);
        }

        [Theory]
        [InlineData("arg`malicious")]
        [InlineData("arg$dangerous")]
        [InlineData("arg%temp%")]
        [InlineData("arg^hacked")]
        [InlineData("arg@evil")]
        [InlineData("arg#bad")]
        [InlineData("arg~test")]
        [InlineData("arg*wildcard")]
        [InlineData("arg?question")]
        [InlineData("arg!exclaim")]
        public void IsValidInput_ContainsSpecialDangerousChars_ReturnsFalse(string input)
        {
            // Act
            bool result = CommandValidator.IsValidInput(input);

            // Assert
            Assert.False(result);
        }

        [Theory]
        [InlineData("--config 'path/to/config.json'")]
        [InlineData("-f \"filename with spaces.txt\"")]
        [InlineData("--data \"C:\\Program Files\\App\\data.db\"")]
        public void IsValidInput_QuotedArgumentsWithDangerousContent_ReturnsFalse(string input)
        {
            // Act
            bool result = CommandValidator.IsValidInput(input);

            // Assert
            Assert.False(result);
        }

        [Theory]
        [InlineData("arg\ninjection")]
        [InlineData("arg\rmalicious")]
        [InlineData("arg\tinjection")]
        public void IsValidInput_ContainsControlCharacters_ReturnsFalse(string input)
        {
            // Act
            bool result = CommandValidator.IsValidInput(input);

            // Assert
            Assert.False(result);
        }

        [Theory]
        [InlineData("script.py --option value")]
        [InlineData("node server.js --port 3000")]
        [InlineData("python run.py --debug")]
        [InlineData("java -jar app.jar")]
        public void IsValidInput_ScriptArgumentsWithAllowedCommands_ReturnsTrue(string input)
        {
            // Act
            bool result = CommandValidator.IsValidInput(input);

            // Assert
            Assert.True(result);
        }

        [Theory]
        [InlineData("path\\to\\file.txt")]
        [InlineData("C:\\App\\config.xml")]
        [InlineData("./relative/path.txt")]
        [InlineData("../parent/file.txt")]
        [InlineData("~/user/file.txt")]
        public void IsValidInput_FilePaths_ReturnsTrue(string input)
        {
            // Act
            bool result = CommandValidator.IsValidInput(input);

            // Assert
            Assert.True(result);
        }

        [Theory]
        [InlineData("http://example.com/api")]
        [InlineData("https://api.service.com/v1/data")]
        [InlineData("ftp://files.server.com/data")]
        [InlineData("ws://localhost:8080/ws")]
        public void IsValidInput_URLs_ReturnsTrue(string input)
        {
            // Act
            bool result = CommandValidator.IsValidInput(input);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void IsValidInput_LargeInputString_HandlesWithoutException()
        {
            // Arrange
            string largeInput = new string('a', 10000);

            // Act & Assert
            Assert.True(CommandValidator.IsValidInput(largeInput));
        }

        #endregion
    }
}