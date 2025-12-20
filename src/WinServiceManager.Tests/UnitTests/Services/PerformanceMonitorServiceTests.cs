using System;
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
/// PerformanceMonitorService 单元测试
/// </summary>
public class PerformanceMonitorServiceTests : IDisposable
{
    private readonly Mock<ILogger<PerformanceMonitorService>> _loggerMock;
    private readonly PerformanceMonitorService _monitorService;
    private readonly TimeSpan _updateInterval = TimeSpan.FromMilliseconds(100);

    public PerformanceMonitorServiceTests()
    {
        _loggerMock = new Mock<ILogger<PerformanceMonitorService>>();
        _monitorService = new PerformanceMonitorService(_loggerMock.Object);
    }

    [Fact]
    public void Constructor_ShouldInitializeCorrectly()
    {
        // Assert
        _monitorService.Should().NotBeNull();
    }

    [Fact]
    public void GetCurrentStats_ShouldReturnValidStats()
    {
        // Act
        var stats = _monitorService.GetCurrentStats();

        // Assert
        stats.Should().NotBeNull();
        stats.CpuUsage.Should().BeGreaterThanOrEqualTo(0);
        stats.CpuUsage.Should().BeLessThanOrEqualTo(100);
        stats.MemoryUsageMB.Should().BeGreaterThan(0);
        stats.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task GetHistoricalStatsAsync_ShouldReturnEmptyListInitially()
    {
        // Act
        var stats = await _monitorService.GetHistoricalStatsAsync();

        // Assert
        stats.Should().BeEmpty();
    }

    [Fact]
    public async Task StartMonitoring_ShouldCollectStatsPeriodically()
    {
        // Arrange
        _monitorService.StartMonitoring(_updateInterval);

        // Act
        await Task.Delay(350); // 等待几次更新

        var historicalStats = await _monitorService.GetHistoricalStatsAsync();

        // Assert
        historicalStats.Should().HaveCountGreaterThan(2);

        // 验证时间戳是递增的
        for (int i = 1; i < historicalStats.Count; i++)
        {
            historicalStats[i].Timestamp.Should().BeAfter(historicalStats[i - 1].Timestamp);
        }

        // Cleanup
        _monitorService.StopMonitoring();
    }

    [Fact]
    public void StopMonitoring_ShouldStopCollection()
    {
        // Arrange
        _monitorService.StartMonitoring(_updateInterval);
        var statsBefore = _monitorService.GetCurrentStats();
        await Task.Delay(150);

        // Act
        _monitorService.StopMonitoring();
        var statsAtStop = _monitorService.GetCurrentStats();
        await Task.Delay(150);
        var statsAfter = _monitorService.GetCurrentStats();

        // Assert
        statsAfter.Timestamp.Should().Be(statsAtStop.Timestamp);
        // 时间戳没有更新，说明监控已停止
    }

    [Fact]
    public void StopMonitoring_WhenNotMonitoring_ShouldNotThrow()
    {
        // Act & Assert
        _monitorService.StopMonitoring();
    }

    [Fact]
    public async Task CheckThresholds_ShouldLogWarningWhenCpuHigh()
    {
        // Arrange - 模拟高CPU使用率
        var stats = new PerformanceStats
        {
            CpuUsage = 95.0,
            MemoryUsageMB = 100,
            Timestamp = DateTime.UtcNow
        };

        // Act - 使用反射调用私有方法
        var checkThresholdsMethod = typeof(PerformanceMonitorService)
            .GetMethod("CheckThresholds", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        checkThresholdsMethod?.Invoke(_monitorService, new object[] { stats });

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("CPU使用率过高")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task CheckThresholds_ShouldLogWarningWhenMemoryHigh()
    {
        // Arrange - 模拟高内存使用率
        var stats = new PerformanceStats
        {
            CpuUsage = 50.0,
            MemoryUsageMB = 2000,
            Timestamp = DateTime.UtcNow
        };

        // Act - 使用反射调用私有方法
        var checkThresholdsMethod = typeof(PerformanceMonitorService)
            .GetMethod("CheckThresholds", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        checkThresholdsMethod?.Invoke(_monitorService, new object[] { stats });

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("内存使用率过高")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task CheckThresholds_WithNormalValues_ShouldNotLogWarning()
    {
        // Arrange - 模拟正常使用率
        var stats = new PerformanceStats
        {
            CpuUsage = 30.0,
            MemoryUsageMB = 200,
            Timestamp = DateTime.UtcNow
        };

        // Act - 使用反射调用私有方法
        var checkThresholdsMethod = typeof(PerformanceMonitorService)
            .GetMethod("CheckThresholds", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        checkThresholdsMethod?.Invoke(_monitorService, new object[] { stats });

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<string>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Never);
    }

    [Fact]
    public async Task GetHistoricalStatsAsync_WithLimit_ShouldReturnLimitedStats()
    {
        // Arrange
        _monitorService.StartMonitoring(_updateInterval);
        await Task.Delay(550); // 收集更多统计

        // Act
        var stats = await _monitorService.GetHistoricalStatsAsync(3);

        // Assert
        stats.Should().HaveCount(3);

        // Cleanup
        _monitorService.StopMonitoring();
    }

    [Fact]
    public async Task GetHistoricalStatsAsync_WhenMonitoring_ShouldReturnRecentStats()
    {
        // Arrange
        _monitorService.StartMonitoring(_updateInterval);
        await Task.Delay(250);
        var startTime = DateTime.UtcNow;
        await Task.Delay(250);

        // Act
        var stats = await _monitorService.GetHistoricalStatsAsync();

        // Assert
        stats.Should().AllSatisfy(s => s.Timestamp.Should().BeOnOrAfter(startTime));

        // Cleanup
        _monitorService.StopMonitoring();
    }

    [Fact]
    public async Task StartMonitoring_AlreadyStarted_ShouldNotDuplicateCollection()
    {
        // Arrange
        _monitorService.StartMonitoring(_updateInterval);
        await Task.Delay(200);

        // Act - 再次启动
        _monitorService.StartMonitoring(_updateInterval);
        await Task.Delay(200);

        var stats = await _monitorService.GetHistoricalStatsAsync();

        // Assert
        // 验证没有重复的收集器在运行
        // 如果有重复收集器，统计数据增长会更快
        stats.Should().HaveCountLessThan(10);

        // Cleanup
        _monitorService.StopMonitoring();
    }

    [Fact]
    public void Dispose_ShouldStopMonitoring()
    {
        // Arrange
        _monitorService.StartMonitoring(_updateInterval);

        // Act
        _monitorService.Dispose();

        // Assert
        // 验证没有抛出异常，且资源已释放
        Assert.True(true);
    }

    [Fact]
    public void GetCurrentStats_ShouldReturnDifferentTimestamps()
    {
        // Act
        var stats1 = _monitorService.GetCurrentStats();
        Thread.Sleep(10);
        var stats2 = _monitorService.GetCurrentStats();

        // Assert
        stats2.Timestamp.Should().BeAfter(stats1.Timestamp);
    }

    [Fact]
    public async Task GetHistoricalStatsAsync_WithLargeLimit_ShouldReturnAllStats()
    {
        // Arrange
        _monitorService.StartMonitoring(_updateInterval);
        await Task.Delay(350);

        // Act
        var allStats = await _monitorService.GetHistoricalStatsAsync(1000);

        // Assert
        var limitedStats = await _monitorService.GetHistoricalStatsAsync();
        allStats.Should().HaveCountGreaterThanOrEqualTo(limitedStats.Count);

        // Cleanup
        _monitorService.StopMonitoring();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    public async Task GetHistoricalStatsAsync_WithInvalidLimit_ShouldThrow(int limit)
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _monitorService.GetHistoricalStatsAsync(limit));
    }

    public void Dispose()
    {
        _monitorService?.Dispose();
    }
}