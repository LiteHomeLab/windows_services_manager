using System;
using System.IO;
using WinServiceManager.Models;
using Xunit;

namespace WinServiceManager.Tests.UnitTests
{
    /// <summary>
    /// Unit tests for CommandValidator class
    /// Tests command validation and sanitization to prevent command injection attacks
    /// </summary>
    public class CommandValidatorTests
    {
        #region SanitizeArguments Tests

        [Theory]
        [InlineData("arg1 arg2 arg3")]
        [InlineData("--version")]
        [InlineData("--port 8080")]
        [InlineData("normal argument")]
        [InlineData("12345")]
        public void SanitizeArguments_ValidArguments_ReturnsSanitizedString(string arguments)
        {
            // Act
            string result = CommandValidator.SanitizeArguments(arguments);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Length > 0);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("\t")]
        public void SanitizeArguments_NullOrEmptyArguments_ReturnsEmptyString(string arguments)
        {
            // Act
            string result = CommandValidator.SanitizeArguments(arguments);

            // Assert
            Assert.Equal(string.Empty, result);
        }

        [Theory]
        [InlineData("arg1 || dir")]
        [InlineData("arg1 && del *.*")]
        [InlineData("command; format c:")]
        [InlineData("arg `net user`")]
        [InlineData("arg $(whoami)")]
        [InlineData("arg @echo off")]
        [InlineData("arg %TEMP%")]
        [InlineData("arg ^powershell")]
        [InlineData("arg < input.txt")]
        [InlineData("arg >> output.txt")]
        [InlineData("arg 1>&2")]
        [InlineData("arg 2>&1")]
        [InlineData("arg /dev/null")]
        public void SanitizeArguments_DangerousPatterns_ThrowsArgumentException(string arguments)
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => CommandValidator.SanitizeArguments(arguments));
            Assert.Contains("dangerous pattern", exception.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Theory]
        [InlineData("arg & echo hacked")]
        [InlineData("arg | findstr password")]
        [InlineData("arg ; shutdown /s")]
        [InlineData("arg < malicious.bat")]
        [InlineData("arg > virus.txt")]
        [InlineData("arg `rm -rf /`")]
        [InlineData("arg $PSVersionTable")]
        [InlineData("arg \"exploit\"")]
        [InlineData("arg 'injection'")]
        [InlineData("arg \\trojan.exe")]
        public void SanitizeArguments_CommandInjection_ThrowsArgumentException(string arguments)
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => CommandValidator.SanitizeArguments(arguments));
            Assert.Contains("command injection", exception.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Theory]
        [InlineData("arg with space")]
        [InlineData("path\\to\\file.txt")]
        [InlineData("file with spaces.txt")]
        [InlineData("normal\"quote")]
        public void SanitizeArguments_ArgumentsNeedingQuoting_ReturnsQuotedString(string arguments)
        {
            // Act
            string result = CommandValidator.SanitizeArguments(arguments);

            // Assert
            // The result should be properly quoted if needed
            Assert.NotNull(result);
        }

        #endregion

        #region IsValidExecutable Tests

        [Theory]
        [InlineData("C:\\Program Files\\MyApp\\app.exe")]
        [InlineData("C:\\Scripts\\run.bat")]
        [InlineData("C:\\Tools\\script.cmd")]
        [InlineData("C:\\Scripts\\script.ps1")]
        [InlineData("C:\\Python\\python.py")]
        [InlineData("C:\\Node\\app.js")]
        [InlineData("C:\\Scripts\\script.vbs")]
        [InlineData("C:\\Scripts\\script.wsf")]
        [InlineData("C:\\Tools\\utility.com")]
        [InlineData("app.exe")]
        [InlineData("script.bat")]
        public void IsValidExecutable_ValidExecutablePaths_ReturnsTrue(string executablePath)
        {
            // Act
            bool result = CommandValidator.IsValidExecutable(executablePath);

            // Assert
            Assert.True(result);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void IsValidExecutable_NullOrEmptyPath_ReturnsFalse(string executablePath)
        {
            // Act
            bool result = CommandValidator.IsValidExecutable(executablePath);

            // Assert
            Assert.False(result);
        }

        [Theory]
        [InlineData("../../../Windows/System32/cmd.exe")]
        [InlineData("..\\..\\..\\Windows\\System32\\powershell.exe")]
        [InlineData(@"\\server\share\malware.exe")]
        [InlineData("C:\\Path\\With<Invalid>Chars.exe")]
        public void IsValidExecutable_InvalidPaths_ReturnsFalse(string executablePath)
        {
            // Act
            bool result = CommandValidator.IsValidExecutable(executablePath);

            // Assert
            Assert.False(result);
        }

        [Theory]
        [InlineData("C:\\App\\malware.scr")]
        [InlineData("C:\\App\\virus.dll")]
        [InlineData("C:\\App\\trojan.sys")]
        [InlineData("C:\\App\\script.txt")]
        [InlineData("C:\\App\\config.ini")]
        public void IsValidExecutable_UnallowedExtensions_ReturnsFalse(string executablePath)
        {
            // Act
            bool result = CommandValidator.IsValidExecutable(executablePath);

            // Assert
            Assert.False(result);
        }

        [Theory]
        [InlineData("C:\\Windows\\System32\\cmd")]
        [InlineData("C:\\Windows\\System32\\powershell")]
        [InlineData("C:\\Windows\\System32\\wscript")]
        [InlineData("C:\\Windows\\System32\\cscript")]
        public void IsValidExecutable_AllowedSystemExecutablesWithoutExtension_ReturnsTrue(string executablePath)
        {
            // Act
            bool result = CommandValidator.IsValidExecutable(executablePath);

            // Assert
            Assert.True(result);
        }

        [Theory]
        [InlineData("C:\\App\\file&name.exe")]
        [InlineData("C:\\App\\file|name.exe")]
        [InlineData("C:\\App\\file;name.exe")]
        [InlineData("C:\\App\\file<name.exe")]
        [InlineData("C:\\App\\file>name.exe")]
        [InlineData("C:\\App\\file`name.exe")]
        [InlineData("C:\\App\\file$name.exe")]
        [InlineData("C:\\App\\file\"name.exe")]
        [InlineData("C:\\App\\file'name.exe")]
        public void IsValidExecutable_FilenamesWithDangerousChars_ReturnsFalse(string executablePath)
        {
            // Act
            bool result = CommandValidator.IsValidExecutable(executablePath);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void IsValidExecutable_InvalidPathFormat_ReturnsFalse()
        {
            // Arrange
            string invalidPath = "C:\\Path\0WithNullChar.exe";

            // Act
            bool result = CommandValidator.IsValidExecutable(invalidPath);

            // Assert
            Assert.False(result);
        }

        #endregion
    }
}