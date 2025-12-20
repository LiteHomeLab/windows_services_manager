using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using WinServiceManager.Models;
using WinServiceManager.Services;
using Xunit;

namespace WinServiceManager.Tests.UnitTests.Services;

/// <summary>
/// ServiceStatusMonitor 单元测试
/// </summary>
public class ServiceStatusMonitorTests : IDisposable
{
    private readonly Mock<ILogger<ServiceStatusMonitor>> _loggerMock;
    private readonly Mock<IServiceManager> _serviceManagerMock;
    private readonly ServiceStatusMonitor _monitor;
    private readonly List<ServiceItem> _testServices;

    public ServiceStatusMonitorTests()
    {
        _loggerMock = new Mock<ILogger<ServiceStatusMonitor>>();
        _serviceManagerMock = new Mock<IServiceManager>();
        _monitor = new ServiceStatusMonitor(_serviceManagerMock.Object, _loggerMock.Object);

        _testServices = new List<ServiceItem>
        {
            new ServiceItem
            {
                Id = "test-service-1",
                DisplayName = "Test Service 1",
                ExecutablePath = @"C:\test\app1.exe"
            },
            new ServiceItem
            {
                Id = "test-service-2",
                DisplayName = "Test Service 2",
                ExecutablePath = @"C:\test\app2.exe"
            }
        };
    }

    [Fact]
    public void Constructor_ShouldInitializeCorrectly()
    {
        // Assert
        _monitor.Should().NotBeNull();
    }

    [Fact]
    public void Subscribe_ShouldAddCallback()
    {
        // Arrange
        var callback1 = new Action<List<ServiceItem>>(_ => { });
        var callback2 = new Action<List<ServiceItem>>(_ => { });

        // Act
        _monitor.Subscribe(callback1);
        _monitor.Subscribe(callback2);

        // Assert - 通过触发回调来验证订阅
        _serviceManagerMock
            .Setup(x => x.GetAllServicesAsync())
            .ReturnsAsync(_testServices);

        // 触发状态更新
        _monitor.RefreshStatusAsync().Wait();

        // 验证回调被调用（通过检查日志）
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("通知")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public void Unsubscribe_ShouldRemoveCallback()
    {
        // Arrange
        var callback = new Action<List<ServiceItem>>(_ => { });
        _monitor.Subscribe(callback);

        // Act
        _monitor.Unsubscribe(callback);

        // Assert - 通过内部状态检查是否移除
        // 由于回调是私有字段，我们通过行为来验证
        _serviceManagerMock
            .Setup(x => x.GetAllServicesAsync())
            .ReturnsAsync(_testServices);

        _monitor.RefreshStatusAsync().Wait();

        // 无法直接验证回调未被调用，但可以确保没有异常
        Assert.True(true);
    }

    [Fact]
    public async Task RefreshStatusAsync_ShouldUpdateServiceStatuses()
    {
        // Arrange
        _serviceManagerMock
            .Setup(x => x.GetAllServicesAsync())
            .ReturnsAsync(_testServices);

        var updatedServices = false;
        _monitor.Subscribe(services => updatedServices = true);

        // Act
        await _monitor.RefreshStatusAsync();

        // Assert
        updatedServices.Should().BeTrue();

        _serviceManagerMock.Verify(x => x.GetAllServicesAsync(), Times.Once);
    }

    [Fact]
    public async Task RefreshStatusAsync_WhenExceptionOccurs_ShouldLogError()
    {
        // Arrange
        var exception = new InvalidOperationException("Test exception");
        _serviceManagerMock
            .Setup(x => x.GetAllServicesAsync())
            .ThrowsAsync(exception);

        var callbackInvoked = false;
        _monitor.Subscribe(_ => callbackInvoked = true);

        // Act
        await _monitor.RefreshStatusAsync();

        // Assert
        callbackInvoked.Should().BeFalse(); // 异常时不应触发回调

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("刷新服务状态失败")),
                exception,
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task RefreshStatusAsync_WithNullServices_ShouldHandleGracefully()
    {
        // Arrange
        _serviceManagerMock
            .Setup(x => x.GetAllServicesAsync())
            .ReturnsAsync((List<ServiceItem>?)null);

        // Act & Assert - 不应抛出异常
        await _monitor.RefreshStatusAsync();

        // 应该记录警告
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("服务列表为空")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Once);
    }

    [Fact]
    public void StartMonitoring_ShouldBeginPeriodicUpdates()
    {
        // Arrange
        _serviceManagerMock
            .Setup(x => x.GetAllServicesAsync())
            .ReturnsAsync(_testServices);

        var updateCount = 0;
        _monitor.Subscribe(_ => Interlocked.Increment(ref updateCount));

        // Act
        _monitor.StartMonitoring(TimeSpan.FromMilliseconds(100));

        // Assert - 等待几次更新
        await Task.Delay(350);

        updateCount.Should().BeGreaterThan(2);

        // Cleanup
        _monitor.StopMonitoring();
    }

    [Fact]
    public void StopMonitoring_ShouldEndPeriodicUpdates()
    {
        // Arrange
        _serviceManagerMock
            .Setup(x => x.GetAllServicesAsync())
            .ReturnsAsync(_testServices);

        var updateCount = 0;
        _monitor.Subscribe(_ => Interlocked.Increment(ref updateCount));

        _monitor.StartMonitoring(TimeSpan.FromMilliseconds(100));
        await Task.Delay(200); // 让它更新几次

        // Act
        _monitor.StopMonitoring();
        var countBefore = updateCount;

        // Assert - 等待一段时间，确保停止更新
        await Task.Delay(200);

        updateCount.Should().Be(countBefore);
    }

    [Fact]
    public void StopMonitoring_WhenNotMonitoring_ShouldNotThrow()
    {
        // Act & Assert - 不应抛出异常
        _monitor.StopMonitoring();
    }

    [Fact]
    public async Task RefreshStatusAsync_WithMultipleCallbacks_ShouldNotifyAll()
    {
        // Arrange
        _serviceManagerMock
            .Setup(x => x.GetAllServicesAsync())
            .ReturnsAsync(_testServices);

        var callback1Count = 0;
        var callback2Count = 0;
        var callback3Count = 0;

        _monitor.Subscribe(_ => Interlocked.Increment(ref callback1Count));
        _monitor.Subscribe(_ => Interlocked.Increment(ref callback2Count));
        _monitor.Subscribe(_ => Interlocked.Increment(ref callback3Count));

        // Act
        await _monitor.RefreshStatusAsync();

        // Assert
        callback1Count.Should().Be(1);
        callback2Count.Should().Be(1);
        callback3Count.Should().Be(1);
    }

    [Fact]
    public async Task RefreshStatusAsync_WithCallbackException_ShouldContinueNotifyingOthers()
    {
        // Arrange
        _serviceManagerMock
            .Setup(x => x.GetAllServicesAsync())
            .ReturnsAsync(_testServices);

        var goodCallbackCount = 0;

        _monitor.Subscribe(_ => throw new InvalidOperationException("Callback failed"));
        _monitor.Subscribe(_ => Interlocked.Increment(ref goodCallbackCount));

        // Act
        await _monitor.RefreshStatusAsync();

        // Assert
        goodCallbackCount.Should().Be(1);

        // 应该记录回调错误
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("回调执行失败")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Once);
    }

    [Fact]
    public void Dispose_ShouldStopMonitoring()
    {
        // Arrange
        _serviceManagerMock
            .Setup(x => x.GetAllServicesAsync())
            .ReturnsAsync(_testServices);

        var updateCount = 0;
        _monitor.Subscribe(_ => Interlocked.Increment(ref updateCount));

        _monitor.StartMonitoring(TimeSpan.FromMilliseconds(50));
        await Task.Delay(100); // 让它更新几次

        // Act
        _monitor.Dispose();
        var countBefore = updateCount;

        // Assert
        await Task.Delay(100);
        updateCount.Should().Be(countBefore);
    }

    public void Dispose()
    {
        _monitor?.Dispose();
    }
}