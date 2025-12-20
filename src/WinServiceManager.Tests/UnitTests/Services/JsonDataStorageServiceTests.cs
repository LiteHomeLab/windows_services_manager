using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using WinServiceManager.Models;
using WinServiceManager.Services;
using Xunit;

namespace WinServiceManager.Tests.UnitTests.Services;

/// <summary>
/// JsonDataStorageService 单元测试
/// </summary>
public class JsonDataStorageServiceTests : IDisposable
{
    private readonly Mock<ILogger<JsonDataStorageService>> _loggerMock;
    private readonly string _testDirectory;
    private readonly string _dataFilePath;
    private readonly JsonDataStorageService _storageService;
    private readonly List<ServiceItem> _testServices;

    public JsonDataStorageServiceTests()
    {
        _loggerMock = new Mock<ILogger<JsonDataStorageService>>();
        _testDirectory = Path.Combine(Path.GetTempPath(), "WinServiceManagerTests", Guid.NewGuid().ToString());
        _dataFilePath = Path.Combine(_testDirectory, "services.json");

        // 创建测试目录
        Directory.CreateDirectory(_testDirectory);

        _storageService = new JsonDataStorageService(_dataFilePath, _loggerMock.Object);

        _testServices = new List<ServiceItem>
        {
            new ServiceItem
            {
                Id = "test-service-1",
                DisplayName = "Test Service 1",
                Description = "Test Description 1",
                ExecutablePath = @"C:\test\app1.exe",
                WorkingDirectory = @"C:\test",
                CreatedAt = DateTime.UtcNow.AddDays(-1)
            },
            new ServiceItem
            {
                Id = "test-service-2",
                DisplayName = "Test Service 2",
                Description = "Test Description 2",
                ExecutablePath = @"C:\test\app2.exe",
                WorkingDirectory = @"C:\test",
                CreatedAt = DateTime.UtcNow
            }
        };
    }

    [Fact]
    public void Constructor_WithValidPath_ShouldInitializeCorrectly()
    {
        // Assert
        _storageService.Should().NotBeNull();
    }

    [Fact]
    public async Task LoadServicesAsync_WhenFileDoesNotExist_ShouldReturnEmptyList()
    {
        // Arrange
        var nonExistentPath = Path.Combine(_testDirectory, "nonexistent.json");
        var service = new JsonDataStorageService(nonExistentPath, _loggerMock.Object);

        // Act
        var result = await service.LoadServicesAsync();

        // Assert
        result.Should().BeEmpty();

        // 应该记录信息日志
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("数据文件不存在")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task LoadServicesAsync_WithValidFile_ShouldReturnServices()
    {
        // Arrange
        await SaveTestServicesAsync();

        // Act
        var result = await _storageService.LoadServicesAsync();

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(s => s.Id == "test-service-1");
        result.Should().Contain(s => s.Id == "test-service-2");
    }

    [Fact]
    public async Task LoadServicesAsync_WithInvalidJson_ShouldReturnEmptyList()
    {
        // Arrange
        await File.WriteAllTextAsync(_dataFilePath, "invalid json content");

        // Act
        var result = await _storageService.LoadServicesAsync();

        // Assert
        result.Should().BeEmpty();

        // 应该记录错误日志
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("解析数据文件失败")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SaveServicesAsync_ShouldCreateFileWithServices()
    {
        // Act
        await _storageService.SaveServicesAsync(_testServices);

        // Assert
        File.Exists(_dataFilePath).Should().BeTrue();

        var json = await File.ReadAllTextAsync(_dataFilePath);
        json.Should().Contain("test-service-1");
        json.Should().Contain("test-service-2");
    }

    [Fact]
    public async Task SaveServicesAsync_ShouldCreateBackupBeforeSave()
    {
        // Arrange
        await _storageService.SaveServicesAsync(_testServices);
        await Task.Delay(100); // 确保文件时间戳不同

        // Act
        var updatedServices = new List<ServiceItem>(_testServices)
        {
            new ServiceItem
            {
                Id = "test-service-3",
                DisplayName = "Test Service 3",
                ExecutablePath = @"C:\test\app3.exe"
            }
        };
        await _storageService.SaveServicesAsync(updatedServices);

        // Assert
        var backupFiles = Directory.GetFiles(_testDirectory, "services.json.backup.*");
        backupFiles.Should().HaveCount(1);
    }

    [Fact]
    public async Task SaveServicesAsync_WhenDirectoryDoesNotExist_ShouldCreateDirectory()
    {
        // Arrange
        var newPath = Path.Combine(_testDirectory, "subdir", "services.json");
        var service = new JsonDataStorageService(newPath, _loggerMock.Object);

        // Act
        await service.SaveServicesAsync(_testServices);

        // Assert
        File.Exists(newPath).Should().BeTrue();
    }

    [Fact]
    public async Task AddServiceAsync_ShouldAddServiceToStorage()
    {
        // Arrange
        await _storageService.SaveServicesAsync(new List<ServiceItem> { _testServices[0] });

        // Act
        await _storageService.AddServiceAsync(_testServices[1]);

        // Assert
        var services = await _storageService.LoadServicesAsync();
        services.Should().HaveCount(2);
        services.Should().Contain(s => s.Id == _testServices[1].Id);
    }

    [Fact]
    public async Task UpdateServiceAsync_WithExistingService_ShouldUpdate()
    {
        // Arrange
        await _storageService.SaveServicesAsync(_testServices);
        var updatedService = new ServiceItem
        {
            Id = _testServices[0].Id,
            DisplayName = "Updated Service Name",
            Description = "Updated Description",
            ExecutablePath = _testServices[0].ExecutablePath
        };

        // Act
        await _storageService.UpdateServiceAsync(updatedService);

        // Assert
        var services = await _storageService.LoadServicesAsync();
        services.Should().HaveCount(2);
        var service = services.First(s => s.Id == updatedService.Id);
        service.DisplayName.Should().Be("Updated Service Name");
        service.Description.Should().Be("Updated Description");
    }

    [Fact]
    public async Task UpdateServiceAsync_WithNonExistingService_ShouldAdd()
    {
        // Arrange
        await _storageService.SaveServicesAsync(new List<ServiceItem> { _testServices[0] });
        var newService = new ServiceItem
        {
            Id = "new-service",
            DisplayName = "New Service",
            ExecutablePath = @"C:\test\new.exe"
        };

        // Act
        await _storageService.UpdateServiceAsync(newService);

        // Assert
        var services = await _storageService.LoadServicesAsync();
        services.Should().HaveCount(2);
        services.Should().Contain(s => s.Id == "new-service");
    }

    [Fact]
    public async Task DeleteServiceAsync_WithExistingService_ShouldRemove()
    {
        // Arrange
        await _storageService.SaveServicesAsync(_testServices);

        // Act
        await _storageService.DeleteServiceAsync(_testServices[0].Id);

        // Assert
        var services = await _storageService.LoadServicesAsync();
        services.Should().HaveCount(1);
        services.Should().NotContain(s => s.Id == _testServices[0].Id);
    }

    [Fact]
    public async Task DeleteServiceAsync_WithNonExistingService_ShouldNotThrow()
    {
        // Arrange
        await _storageService.SaveServicesAsync(_testServices);

        // Act & Assert
        await _storageService.DeleteServiceAsync("non-existing-id");

        var services = await _storageService.LoadServicesAsync();
        services.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetServiceAsync_WithExistingId_ShouldReturnService()
    {
        // Arrange
        await _storageService.SaveServicesAsync(_testServices);

        // Act
        var result = await _storageService.GetServiceAsync(_testServices[1].Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(_testServices[1].Id);
        result.DisplayName.Should().Be(_testServices[1].DisplayName);
    }

    [Fact]
    public async Task GetServiceAsync_WithNonExistingId_ShouldReturnNull()
    {
        // Arrange
        await _storageService.SaveServicesAsync(_testServices);

        // Act
        var result = await _storageService.GetServiceAsync("non-existing-id");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task ConcurrentAccess_ShouldHandleCorrectly()
    {
        // Arrange
        var tasks = new List<Task>();

        // Act - 并发写入和读取
        for (int i = 0; i < 10; i++)
        {
            var service = new ServiceItem
            {
                Id = $"concurrent-service-{i}",
                DisplayName = $"Concurrent Service {i}",
                ExecutablePath = $@"C:\test\concurrent{i}.exe"
            };

            tasks.Add(_storageService.AddServiceAsync(service));
            tasks.Add(_storageService.LoadServicesAsync());
        }

        // 等待所有任务完成
        await Task.WhenAll(tasks);

        // Assert
        var services = await _storageService.LoadServicesAsync();
        services.Should().HaveCount(10);
    }

    [Fact]
    public async Task SaveServicesAsync_WithUnauthorizedAccess_ShouldLogError()
    {
        // Arrange
        var readOnlyFile = Path.Combine(_testDirectory, "readonly.json");
        File.WriteAllText(readOnlyFile, "{}");
        var fileAttributes = File.GetAttributes(readOnlyFile);
        File.SetAttributes(readOnlyFile, FileAttributes.ReadOnly);

        var service = new JsonDataStorageService(readOnlyFile, _loggerMock.Object);

        // Act
        await service.SaveServicesAsync(_testServices);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("保存数据失败")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Once);

        // Cleanup
        File.SetAttributes(readOnlyFile, fileAttributes);
    }

    [Fact]
    public async Task GetServiceAsync_WithNullId_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _storageService.GetServiceAsync(null!));
    }

    private async Task SaveTestServicesAsync()
    {
        var options = new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true
        };
        var json = System.Text.Json.JsonSerializer.Serialize(_testServices, options);
        await File.WriteAllTextAsync(_dataFilePath, json);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDirectory))
            {
                Directory.Delete(_testDirectory, true);
            }
        }
        catch
        {
            // 忽略清理错误
        }
    }
}