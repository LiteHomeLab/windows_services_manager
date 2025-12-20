using System;
using System.Collections.Generic;
using FluentAssertions;
using WinServiceManager.Models;
using Xunit;

namespace WinServiceManager.Tests.UnitTests;

/// <summary>
/// ServiceItem 单元测试
/// </summary>
public class ServiceItemTests
{
    [Fact]
    public void Constructor_ShouldInitializeWithDefaults()
    {
        // Act
        var service = new ServiceItem();

        // Assert
        service.Id.Should().NotBeNullOrEmpty();
        service.DisplayName.Should().BeEmpty();
        service.Description.Should().Be("Managed by WinServiceManager");
        service.ExecutablePath.Should().BeEmpty();
        service.ScriptPath.Should().BeNull();
        service.WorkingDirectory.Should().BeEmpty();
        service.StartupArguments.Should().BeEmpty();
        service.ServiceAccount.Should().Be("LocalSystem");
        service.Environment.Should().BeNull();
        service.LogPath.Should().BeEmpty();
        service.LogMode.Should().Be(LogMode.Reset);
        service.StartMode.Should().Be(ServiceStartMode.Automatic);
        service.StopTimeout.Should().Be(15000);
        service.Priority.Should().Be(ProcessPriority.Normal);
        service.Affinity.Should().Be("All");
        service.Metadata.Should().BeNull();
        service.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Constructor_WithId_ShouldSetId()
    {
        // Arrange
        var testId = "test-service-id";

        // Act
        var service = new ServiceItem { Id = testId };

        // Assert
        service.Id.Should().Be(testId);
    }

    [Fact]
    public void ExecutablePath_WithInvalidPath_ShouldThrowArgumentException()
    {
        // Arrange
        var service = new ServiceItem();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => service.ExecutablePath = "invalid<>path");
    }

    [Fact]
    public void ExecutablePath_WithValidPath_ShouldSet()
    {
        // Arrange
        var service = new ServiceItem();
        var validPath = @"C:\Program Files\test\app.exe";

        // Act
        service.ExecutablePath = validPath;

        // Assert
        service.ExecutablePath.Should().Be(validPath);
    }

    [Fact]
    public void ExecutablePath_WithEmptyValue_ShouldSet()
    {
        // Arrange
        var service = new ServiceItem();

        // Act
        service.ExecutablePath = "";

        // Assert
        service.ExecutablePath.Should().Be("");
    }

    [Fact]
    public void ScriptPath_WithInvalidPath_ShouldThrowArgumentException()
    {
        // Arrange
        var service = new ServiceItem();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => service.ScriptPath = "invalid<>path");
    }

    [Fact]
    public void ScriptPath_WithValidPath_ShouldSet()
    {
        // Arrange
        var service = new ServiceItem();
        var validPath = @"C:\scripts\test.py";

        // Act
        service.ScriptPath = validPath;

        // Assert
        service.ScriptPath.Should().Be(validPath);
    }

    [Fact]
    public void ScriptPath_WithNull_ShouldSet()
    {
        // Arrange
        var service = new ServiceItem();

        // Act
        service.ScriptPath = null;

        // Assert
        service.ScriptPath.Should().BeNull();
    }

    [Fact]
    public void WorkingDirectory_WithInvalidPath_ShouldThrowArgumentException()
    {
        // Arrange
        var service = new ServiceItem();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => service.WorkingDirectory = "invalid<>path");
    }

    [Fact]
    public void WorkingDirectory_WithValidPath_ShouldSet()
    {
        // Arrange
        var service = new ServiceItem();
        var validPath = @"C:\work";

        // Act
        service.WorkingDirectory = validPath;

        // Assert
        service.WorkingDirectory.Should().Be(validPath);
    }

    [Fact]
    public void WorkingDirectory_WithEmpty_ShouldSet()
    {
        // Arrange
        var service = new ServiceItem();

        // Act
        service.WorkingDirectory = "";

        // Assert
        service.WorkingDirectory.Should().Be("");
    }

    [Fact]
    public void GetFullArguments_WithoutScriptPath_ShouldReturnStartupArguments()
    {
        // Arrange
        var service = new ServiceItem
        {
            StartupArguments = "--port 8080",
            ScriptPath = null
        };

        // Act
        var args = service.GetFullArguments();

        // Assert
        args.Should().Be("--port 8080");
    }

    [Fact]
    public void GetFullArguments_WithScriptPath_ShouldIncludeScriptPath()
    {
        // Arrange
        var service = new ServiceItem
        {
            StartupArguments = "--port 8080",
            ScriptPath = @"C:\scripts\test.py"
        };

        // Act
        var args = service.GetFullArguments();

        // Assert
        args.Should().Be("\"C:\\scripts\\test.py\" --port 8080");
    }

    [Fact]
    public void GetFullArguments_WithEmptyStartupArguments_ShouldReturnScriptPathOnly()
    {
        // Arrange
        var service = new ServiceItem
        {
            StartupArguments = "",
            ScriptPath = @"C:\scripts\test.py"
        };

        // Act
        var args = service.GetFullArguments();

        // Assert
        args.Should().Be("\"C:\\scripts\\test.py\"");
    }

    [Fact]
    public void GetFullArguments_WithNullStartupArguments_ShouldReturnScriptPathOnly()
    {
        // Arrange
        var service = new ServiceItem
        {
            StartupArguments = null,
            ScriptPath = @"C:\scripts\test.py"
        };

        // Act
        var args = service.GetFullArguments();

        // Assert
        args.Should().Be("\"C:\\scripts\\test.py\"");
    }

    [Fact]
    public void GetFullArguments_WithoutScriptPathAndNullStartupArguments_ShouldReturnEmpty()
    {
        // Arrange
        var service = new ServiceItem
        {
            StartupArguments = null,
            ScriptPath = null
        };

        // Act
        var args = service.GetFullArguments();

        // Assert
        args.Should().Be("");
    }

    [Fact]
    public void GetFullArguments_WithScriptPathContainingSpaces_ShouldQuotePath()
    {
        // Arrange
        var service = new ServiceItem
        {
            StartupArguments = "--verbose",
            ScriptPath = @"C:\Program Files\My App\script.py"
        };

        // Act
        var args = service.GetFullArguments();

        // Assert
        args.Should().Be("\"C:\\Program Files\\My App\\script.py\" --verbose");
    }

    [Fact]
    public void GenerateWinSWConfig_ShouldCreateValidXml()
    {
        // Arrange
        var service = new ServiceItem
        {
            Id = "test-service",
            DisplayName = "Test Service",
            Description = "Test Description",
            ExecutablePath = @"C:\test\app.exe",
            StartupArguments = "--port 8080",
            WorkingDirectory = @"C:\test",
            ServiceAccount = "LocalService",
            Environment = new Dictionary<string, string>
            {
                ["NODE_ENV"] = "production",
                ["PORT"] = "8080"
            },
            LogPath = @"C:\logs\test.log",
            LogMode = LogMode.Append,
            StartMode = ServiceStartMode.Manual,
            StopTimeout = 30000,
            Priority = ProcessPriority.High,
            Affinity = "0,1",
            Metadata = new Dictionary<string, string>
            {
                ["Version"] = "1.0",
                ["Author"] = "Test"
            }
        };

        // Act
        var config = service.GenerateWinSWConfig();

        // Assert
        config.Should().NotBeNull();
        config.Id.Should().Be("test-service");
        config.Name.Should().Be("Test Service");
        config.Description.Should().Be("Test Description");

        config.Executable.Should().Be(@"C:\test\app.exe");
        config.Arguments.Should().Be("--port 8080");
        config.WorkingDirectory.Should().Be(@"C:\test");

        config.ServiceAccount.Should().Be("LocalService");

        config.Env.Should().HaveCount(2);
        config.Env.Should().ContainKey("NODE_ENV");
        config.Env["NODE_ENV"].Should().Be("production");
        config.Env.Should().ContainKey("PORT");
        config.Env["PORT"].Should().Be("8080");

        config.LogPath.Should().Be(@"C:\logs\test.log");
        config.Logmode.Should().Be("append");

        config.StartMode.Should().Be("manual");
        config.StopTimeout.Should().Be("30000");

        config.Priority.Should().Be("high");
        config.Affinity.Should().Be("0,1");

        config.Should().ContainKey("version");
        config["version"].Should().Be("1.0");
        config.Should().ContainKey("author");
        config["author"].Should().Be("Test");
    }

    [Fact]
    public void GenerateWinSWConfig_WithMinimalConfiguration_ShouldCreateValidXml()
    {
        // Arrange
        var service = new ServiceItem
        {
            Id = "minimal-service",
            DisplayName = "Minimal Service",
            ExecutablePath = @"C:\test\simple.exe"
        };

        // Act
        var config = service.GenerateWinSWConfig();

        // Assert
        config.Should().NotBeNull();
        config.Id.Should().Be("minimal-service");
        config.Name.Should().Be("Minimal Service");
        config.Description.Should().Be("Managed by WinServiceManager");

        config.Executable.Should().Be(@"C:\test\simple.exe");
        config.Arguments.Should().BeNullOrEmpty();

        config.WorkingDirectory.Should().BeNullOrEmpty();
        config.ServiceAccount.Should().Be("LocalSystem");
        config.Env.Should().BeNull();

        config.LogPath.Should().BeNullOrEmpty();
        config.Logmode.Should().Be("reset");

        config.StartMode.Should().Be("automatic");
        config.StopTimeout.Should().Be("15000");

        config.Priority.Should().Be("normal");
        config.Affinity.Should().Be("All");
    }

    [Fact]
    public void GenerateWinSWConfig_WithPythonScript_ShouldCreateCorrectArguments()
    {
        // Arrange
        var service = new ServiceItem
        {
            Id = "python-service",
            DisplayName = "Python Service",
            ExecutablePath = @"C:\Python311\python.exe",
            ScriptPath = @"C:\scripts\app.py",
            StartupArguments = "--debug"
        };

        // Act
        var config = service.GenerateWinSWConfig();

        // Assert
        config.Arguments.Should().Be("\"C:\\scripts\\app.py\" --debug");
    }

    [Fact]
    public void GeneratedWinSWConfig_SerializeToXml_ShouldBeWellFormed()
    {
        // Arrange
        var service = new ServiceItem
        {
            Id = "xml-test",
            DisplayName = "XML Test Service",
            ExecutablePath = @"C:\test\app.exe"
        };

        // Act
        var config = service.GenerateWinSWConfig();
        var xml = config.ToString();

        // Assert
        xml.Should().StartWith("<service>");
        xml.Should().EndWith("</service>");
        xml.Should().Contain("<id>xml-test</id>");
        xml.Should().Contain("<name>XML Test Service</name>");
        xml.Should().Contain("<executable>C:\\test\\app.exe</executable>");
    }

    [Fact]
    public void ServiceItem_WithSpecialCharacters_ShouldBeEscapedInXml()
    {
        // Arrange
        var service = new ServiceItem
        {
            Id = "xml-escape-test",
            DisplayName = "Test <Service> & \"Quotes\"",
            Description = "Service with 'apostrophes' & ampersands",
            ExecutablePath = @"C:\test\app.exe"
        };

        // Act
        var config = service.GenerateWinSWConfig();
        var xml = config.ToString();

        // Assert
        xml.Should().Contain("&lt;Service&gt;");
        xml.Should().Contain("&amp;");
        xml.Should().NotContain("<Service>");
        xml.Should().NotContain("&");
    }
}