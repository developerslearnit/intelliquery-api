using FluentAssertions;
using IntelliQueryAI.Domain.Enums;
using IntelliQueryAI.Domain.Services;
using IntelliQueryAI.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace IntelliQueryAI.Application.Tests;

public sealed class StatisticalAnomalyDetectionServiceTests
{
    private readonly StatisticalAnomalyDetectionService _sut = new(NullLogger<StatisticalAnomalyDetectionService>.Instance);

    [Fact]
    public void DetectAnomaly_WhenValueWithinNormalRange_ShouldNotFlagAnomaly()
    {
        // Arrange: 10 historical values around 100 with small variance
        var history = new List<MetricDataPoint>
        {
            new(DateTime.UtcNow.AddDays(-10), 98m),
            new(DateTime.UtcNow.AddDays(-9), 102m),
            new(DateTime.UtcNow.AddDays(-8), 100m),
            new(DateTime.UtcNow.AddDays(-7), 101m),
            new(DateTime.UtcNow.AddDays(-6), 99m),
            new(DateTime.UtcNow.AddDays(-5), 103m),
            new(DateTime.UtcNow.AddDays(-4), 97m),
            new(DateTime.UtcNow.AddDays(-3), 100m),
            new(DateTime.UtcNow.AddDays(-2), 102m),
            new(DateTime.UtcNow.AddDays(-1), 100m)
        };

        // Act
        var result = _sut.DetectAnomaly(history, currentValue: 101m);

        // Assert
        result.IsAnomaly.Should().BeFalse();
        result.Severity.Should().Be(AlertSeverity.Information);
        result.CurrentValue.Should().Be(101m);
    }

    [Fact]
    public void DetectAnomaly_WhenSuddenDropOccurs_ShouldFlagCriticalDropSpike()
    {
        // Arrange: Mean ~ 100, StdDev ~ 2. Current = 60 (-40% drop)
        var history = new List<MetricDataPoint>
        {
            new(DateTime.UtcNow.AddDays(-5), 100m),
            new(DateTime.UtcNow.AddDays(-4), 102m),
            new(DateTime.UtcNow.AddDays(-3), 98m),
            new(DateTime.UtcNow.AddDays(-2), 101m),
            new(DateTime.UtcNow.AddDays(-1), 99m)
        };

        // Act
        var result = _sut.DetectAnomaly(history, currentValue: 60m, zScoreThreshold: 2.5, percentageThreshold: 20m);

        // Assert
        result.IsAnomaly.Should().BeTrue();
        result.AlertType.Should().Be(AlertType.DropSpike);
        result.Severity.Should().Be(AlertSeverity.Critical);
        result.ZScore.Should().BeLessThan(-3.0);
        result.DeviationPercentage.Should().BeLessThan(-35m);
        result.Description.Should().Contain("downward drop detected");
    }

    [Fact]
    public void DetectAnomaly_WhenCurrentValueIsZeroAndBaselineIsHigh_ShouldDetectInactivity()
    {
        // Arrange
        var history = new List<MetricDataPoint>
        {
            new(DateTime.UtcNow.AddDays(-3), 500m),
            new(DateTime.UtcNow.AddDays(-2), 520m),
            new(DateTime.UtcNow.AddDays(-1), 480m)
        };

        // Act
        var result = _sut.DetectAnomaly(history, currentValue: 0m);

        // Assert
        result.IsAnomaly.Should().BeTrue();
        result.AlertType.Should().Be(AlertType.InactivityWarning);
        result.Severity.Should().Be(AlertSeverity.Critical);
        result.Description.Should().Contain("inactivity detected");
    }

    [Fact]
    public void AnalyzeTrend_WhenValuesIncrease_ShouldReportUpwardTrendWithPositiveGrowth()
    {
        // Arrange: Series growing from 100 to 200
        var series = new List<MetricDataPoint>
        {
            new(DateTime.UtcNow.AddDays(-4), 100m),
            new(DateTime.UtcNow.AddDays(-3), 125m),
            new(DateTime.UtcNow.AddDays(-2), 150m),
            new(DateTime.UtcNow.AddDays(-1), 175m),
            new(DateTime.UtcNow, 200m)
        };

        // Act
        var trend = _sut.AnalyzeTrend(series);

        // Assert
        trend.Direction.Should().Be("Upward");
        trend.GrowthRatePercentage.Should().Be(100.0m);
        trend.AverageValue.Should().Be(150.0m);
        trend.MinValue.Should().Be(100.0m);
        trend.MaxValue.Should().Be(200.0m);
    }
}
