using System;
using System.IO;
using WinServiceManager.Models;
using Xunit;

namespace WinServiceManager.Tests.UnitTests
{
    /// <summary>
    /// Unit tests for PathValidator class
    /// Tests path validation logic to prevent path traversal attacks and ensure security
    /// </summary>
    public class PathValidatorTests
    {
        #region IsValidPath Tests

        [Fact]
        public void IsValidPath_ValidPath_ReturnsTrue()
        {
            // Arrange
            string validPath = Path.Combine(Path.GetTempPath(), "test", "application.exe");

            // Act
            bool result = PathValidator.IsValidPath(validPath);

            // Assert
            Assert.True(result);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("\t")]
        public void IsValidPath_NullOrEmptyPath_ReturnsFalse(string path)
        {
            // Act
            bool result = PathValidator.IsValidPath(path);

            // Assert
            Assert.False(result);
        }

        [Theory]
        [InlineData("../../../Windows/System32/cmd.exe")]
        [InlineData("..\\..\\..\\Windows\\System32\\cmd.exe")]
        [FilePath("../etc/passwd")]
        [FilePath("..\\..\\boot.ini")]
        [InlineData("/../../Windows/System32")]
        [InlineData("\\..\\..\\Windows")]
        [InlineData("folder/../../../secret.txt")]
        [InlineData("folder\\..\\..\\..\\config.ini")]
        public void IsValidPath_PathTraversalAttacks_ReturnsFalse(string path)
        {
            // Act
            bool result = PathValidator.IsValidPath(path);

            // Assert
            Assert.False(result, $"Path '{path}' should be rejected as path traversal attack");
        }

        [Theory]
        [InlineData(@"\\server\share\file.exe")]
        [InlineData(@"\\?\C:\Windows\System32")]
        [InlineData(@"\\.\PhysicalDrive0")]
        [InlineData(@"\\localhost\C$\Windows")]
        public void IsValidPath_UNCPaths_ReturnsFalse(string path)
        {
            // Act
            bool result = PathValidator.IsValidPath(path);

            // Assert
            Assert.False(result, $"UNC path '{path}' should be rejected");
        }

        [Theory]
        [InlineData("C:\\Windows\\System32\\cmd.exe")]
        [InlineData("C:\\Windows\\System32\\powershell.exe")]
        [InlineData("C:\\Windows\\System32\\regedit.exe")]
        [InlineData("C:\\Windows\\System32\\taskmgr.exe")]
        public void IsValidPath_ForbiddenSystemFiles_ReturnsFalse(string path)
        {
            // Act
            bool result = PathValidator.IsValidPath(path);

            // Assert
            Assert.False(result, $"Access to system file '{path}' should be forbidden");
        }

        [Theory]
        [InlineData("C:\\Windows\\System32\\notepad.exe")]
        [InlineData("C:\\Windows\\SysWOW64\\notepad.exe")]
        [InlineData("C:\\Program Files\\MyApp\\app.exe")]
        [InlineData("C:\\Program Files (x86)\\MyApp\\app.exe")]
        public void IsValidPath_AllowedSystemDirectories_ReturnsTrue(string path)
        {
            // Act
            bool result = PathValidator.IsValidPath(path);

            // Assert
            Assert.True(result, $"Access to '{path}' should be allowed");
        }

        [Fact]
        public void IsValidPath_PathExceedsMaxLength_ReturnsFalse()
        {
            // Arrange
            string longPath = new string('a', 300);

            // Act
            bool result = PathValidator.IsValidPath(longPath);

            // Assert
            Assert.False(result);
        }

        [Theory]
        [InlineData("C:\\Path\\With<Invalid>Chars.exe")]
        [InlineData("C:\\Path\\With|Pipe.exe")]
        [InlineData("C:\\Path\\With?Question.exe")]
        [InlineData("C:\\Path\\With*Star.exe")]
        [InlineData("C:\\Path\\With\"Quote.exe")]
        public void IsValidPath_PathWithInvalidChars_ReturnsFalse(string path)
        {
            // Act
            bool result = PathValidator.IsValidPath(path);

            // Assert
            Assert.False(result, $"Path with invalid characters '{path}' should be rejected");
        }

        [Fact]
        public void IsValidPath_InvalidPathFormat_ReturnsFalse()
        {
            // Arrange - Use characters that would cause Path.GetFullPath to throw
            string invalidPath = "C:\\Path\0WithNullChar.exe";

            // Act
            bool result = PathValidator.IsValidPath(invalidPath);

            // Assert
            Assert.False(result);
        }

        #endregion

        #region GetSafePath Tests

        [Fact]
        public void GetSafePath_ValidPath_ReturnsFullPath()
        {
            // Arrange
            string relativePath = "test\\app.exe";

            // Act
            string result = PathValidator.GetSafePath(relativePath);

            // Assert
            Assert.True(Path.IsPathRooted(result));
            Assert.EndsWith("app.exe", result);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("../../../Windows/System32")]
        [InlineData(@"\\server\share")]
        public void GetSafePath_InvalidPath_ThrowsArgumentException(string path)
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() => PathValidator.GetSafePath(path));
        }

        #endregion

        #region IsValidFileName Tests

        [Theory]
        [InlineData("document.txt")]
        [InlineData("application.exe")]
        [InlineData("config.json")]
        [InlineData("script.py")]
        public void IsValidFileName_ValidFileName_ReturnsTrue(string fileName)
        {
            // Act
            bool result = PathValidator.IsValidFileName(fileName);

            // Assert
            Assert.True(result);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void IsValidFileName_NullOrEmptyFileName_ReturnsFalse(string fileName)
        {
            // Act
            bool result = PathValidator.IsValidFileName(fileName);

            // Assert
            Assert.False(result);
        }

        [Theory]
        [InlineData("file<name.txt")]
        [InlineData("file|name.txt")]
        [InlineData("file?name.txt")]
        [InlineData("file*name.txt")]
        [InlineData("file\"name.txt")]
        [InlineData("file:name.txt")]
        [InlineData("file>name.txt")]
        [InlineData("file/name.txt")]
        [InlineData("file\\name.txt")]
        public void IsValidFileName_FileNameWithInvalidChars_ReturnsFalse(string fileName)
        {
            // Act
            bool result = PathValidator.IsValidFileName(fileName);

            // Assert
            Assert.False(result, $"File name with invalid characters '{fileName}' should be rejected");
        }

        [Theory]
        [InlineData("CON")]
        [InlineData("PRN")]
        [InlineData("AUX")]
        [InlineData("NUL")]
        [InlineData("COM1")]
        [InlineData("COM2")]
        [InlineData("COM3")]
        [InlineData("COM4")]
        [InlineData("COM5")]
        [InlineData("COM6")]
        [InlineData("COM7")]
        [InlineData("COM8")]
        [InlineData("COM9")]
        [InlineData("LPT1")]
        [InlineData("LPT2")]
        [InlineData("LPT3")]
        [InlineData("LPT4")]
        [InlineData("LPT5")]
        [InlineData("LPT6")]
        [InlineData("LPT7")]
        [InlineData("LPT8")]
        [InlineData("LPT9")]
        [InlineData("con.txt")]  // Case insensitive with extension
        [InlineData("AUX.log")]  // Case insensitive with extension
        public void IsValidFileName_ReservedNames_ReturnsFalse(string fileName)
        {
            // Act
            bool result = PathValidator.IsValidFileName(fileName);

            // Assert
            Assert.False(result, $"Reserved name '{fileName}' should be rejected");
        }

        [Theory]
        [InlineData("../../../config.txt")]
        [InlineData("..\\..\\secret.ini")]
        [InlineData("file/../config.txt")]
        [InlineData("file\\..\\config.ini")]
        public void IsValidFileName_PathTraversalInFileName_ReturnsFalse(string fileName)
        {
            // Act
            bool result = PathValidator.IsValidFileName(fileName);

            // Assert
            Assert.False(result, $"File name with path traversal '{fileName}' should be rejected");
        }

        #endregion
    }
}