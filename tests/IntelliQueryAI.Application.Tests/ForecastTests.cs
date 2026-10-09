using IntelliQueryAI.AI.Coordinator;
using IntelliQueryAI.Application.Features.Forecasting.Commands;
using IntelliQueryAI.Application.Features.Forecasting.Dtos;
using IntelliQueryAI.Application.Features.Forecasting.Queries;
using IntelliQueryAI.Domain.Entities.Forecasting;
using IntelliQueryAI.Domain.Errors;
using IntelliQueryAI.Domain.Persistence;
using IntelliQueryAI.Domain.Services;
using Moq;
using Xunit;

namespace IntelliQueryAI.Application.Tests;

public class ForecastTests
{
    private readonly ForecastingEngine _engine = new();

    [Fact]
    public void ForecastingEngine_LinearTrend_ShouldComputeAccurateSlopeAndPredictions()
    {
        // Arrange - Perfect linear series: y = 20x + 100
        var historical = new List<HistoricalDataPoint>
        {
            new("Q1 2024", 100m),
            new("Q2 2024", 120m),
            new("Q3 2024", 140m),
            new("Q4 2024", 160m)
        };

        // Act
        var output = _engine.ComputeForecast(historical, ForecastMethod.LinearTrend, horizonSteps: 3, confidenceLevel: 0.95);

        // Assert
        Assert.Equal(3, output.PredictionPoints.Count);
        Assert.Equal(180m, Math.Round(output.PredictionPoints[0].ForecastValue, 2));
        Assert.Equal(200m, Math.Round(output.PredictionPoints[1].ForecastValue, 2));
        Assert.Equal(220m, Math.Round(output.PredictionPoints[2].ForecastValue, 2));

        // Confidence interval envelope checks
        foreach (var point in output.PredictionPoints)
        {
            Assert.True(point.LowerBound <= point.ForecastValue, "Lower bound must be <= forecast value");
            Assert.True(point.UpperBound >= point.ForecastValue, "Upper bound must be >= forecast value");
        }

        Assert.True(output.RSquared > 0.99, "R² should be close to 1 for perfect linear series");
        Assert.NotEmpty(output.Assumptions);
        Assert.True(output.ProjectedGrowthRate > 0, "Projected growth rate should be positive");
    }

    [Fact]
    public void ForecastingEngine_MovingAverage_ShouldProduceSmoothProjections()
    {
        // Arrange
        var historical = new List<HistoricalDataPoint>
        {
            new("Jan", 50m),
            new("Feb", 55m),
            new("Mar", 60m),
            new("Apr", 58m),
            new("May", 62m),
            new("Jun", 65m)
        };

        // Act
        var output = _engine.ComputeForecast(historical, ForecastMethod.MovingAverage, horizonSteps: 3, confidenceLevel: 0.95);

        // Assert
        Assert.Equal(3, output.PredictionPoints.Count);
        foreach (var point in output.PredictionPoints)
        {
            Assert.True(point.ForecastValue > 50m);
            Assert.True(point.LowerBound <= point.ForecastValue);
            Assert.True(point.UpperBound >= point.ForecastValue);
        }
    }

    [Fact]
    public void ForecastingEngine_ExponentialSmoothing_ShouldAccountForLongitudinalTrend()
    {
        // Arrange
        var historical = new List<HistoricalDataPoint>
        {
            new("P1", 100m),
            new("P2", 110m),
            new("P3", 125m),
            new("P4", 140m),
            new("P5", 160m)
        };

        // Act
        var output = _engine.ComputeForecast(historical, ForecastMethod.ExponentialSmoothing, horizonSteps: 2, confidenceLevel: 0.95);

        // Assert
        Assert.Equal(2, output.PredictionPoints.Count);
        Assert.True(output.PredictionPoints[0].ForecastValue > 150m, "Holt linear trend should extrapolate positive trajectory");
        Assert.True(output.PredictionPoints[1].ForecastValue > output.PredictionPoints[0].ForecastValue);
        Assert.True(output.FinalProjectedValue > 160m);
    }

    [Fact]
    public void ForecastingEngine_SeasonalGrowth_ShouldCompoundCAGR()
    {
        // Arrange
        var historical = new List<HistoricalDataPoint>
        {
            new("2021", 1000m),
            new("2022", 1100m),
            new("2023", 1210m) // 10% compound growth per period
        };

        // Act
        var output = _engine.ComputeForecast(historical, ForecastMethod.SeasonalGrowth, horizonSteps: 2, confidenceLevel: 0.95);

        // Assert
        Assert.Equal(2, output.PredictionPoints.Count);
        Assert.True(output.ProjectedGrowthRate > 0m);
        Assert.True(output.PredictionPoints[0].ForecastValue > 1210m);
        Assert.True(output.PredictionPoints[1].ForecastValue > output.PredictionPoints[0].ForecastValue);
    }

    [Fact]
    public void ForecastingEngine_InsufficientPoints_ThrowsArgumentException()
    {
        var historical = new List<HistoricalDataPoint> { new("Q1", 100m) };

        Assert.Throws<ArgumentException>(() =>
            _engine.ComputeForecast(historical, ForecastMethod.LinearTrend, 3));
    }

    [Fact]
    public async Task GenerateForecastCommandHandler_ShouldFail_WhenInsufficientData()
    {
        // Arrange
        var mockCoordinator = new Mock<IForecastCoordinator>();
        var handler = new GenerateForecastCommandHandler(mockCoordinator.Object);

        var command = new GenerateForecastCommand(
            WorkspaceId: Guid.NewGuid(),
            UserId: Guid.NewGuid(),
            Request: new GenerateForecastRequestDto(
                MetricName: "Monthly Active Users",
                HistoricalPeriod: "Jan",
                ForecastPeriod: "Feb",
                Method: "LinearTrend",
                HorizonSteps: 3,
                ConfidenceLevel: 0.95,
                HistoricalPoints: new List<HistoricalDataPointDto> { new("Jan", 100m) }));

        // Act
        var result = await handler.HandleAsync(command);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Forecast.InsufficientHistoricalData", result.Error.Code);
        mockCoordinator.Verify(c => c.GenerateAndSaveForecastAsync(
            It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ForecastMethod>(), It.IsAny<int>(),
            It.IsAny<double>(), It.IsAny<IReadOnlyList<HistoricalDataPoint>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GenerateForecastCommandHandler_Success_ShouldReturnPersistedForecast()
    {
        // Arrange
        var workspaceId = Guid.NewGuid();
        var mockCoordinator = new Mock<IForecastCoordinator>();

        var domainForecast = new Forecast(
            workspaceId: workspaceId,
            createdByUserId: Guid.NewGuid(),
            dataSourceId: null,
            metricName: "ARR",
            forecastPeriod: "Q1-Q3 2025",
            historicalPeriod: "Q1-Q4 2024",
            method: ForecastMethod.LinearTrend,
            confidenceInterval: 0.95,
            historicalPoints: new List<HistoricalDataPoint> { new("Q1", 100m), new("Q2", 120m) },
            forecastPoints: new List<ForecastPredictionPoint> { new("Q3", 140m, 130m, 150m) },
            assumptions: new List<ForecastAssumption> { new("Model", "OLS", "Ordinary Least Squares") },
            projectedGrowthRate: 16.67m,
            finalProjectedValue: 140m,
            aiExplanation: "Executive trajectory analysis");

        mockCoordinator.Setup(c => c.GenerateAndSaveForecastAsync(
            workspaceId,
            It.IsAny<Guid?>(),
            It.IsAny<Guid?>(),
            "ARR",
            It.IsAny<string>(),
            It.IsAny<string>(),
            ForecastMethod.LinearTrend,
            3,
            0.95,
            It.IsAny<IReadOnlyList<HistoricalDataPoint>>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(domainForecast);

        var handler = new GenerateForecastCommandHandler(mockCoordinator.Object);
        var command = new GenerateForecastCommand(
            WorkspaceId: workspaceId,
            UserId: Guid.NewGuid(),
            Request: new GenerateForecastRequestDto(
                MetricName: "ARR",
                HistoricalPeriod: "Q1-Q4 2024",
                ForecastPeriod: "Q1-Q3 2025",
                Method: "LinearTrend",
                HorizonSteps: 3,
                ConfidenceLevel: 0.95,
                HistoricalPoints: new List<HistoricalDataPointDto> { new("Q1", 100m), new("Q2", 120m) }));

        // Act
        var result = await handler.HandleAsync(command);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("ARR", result.Value.MetricName);
        Assert.Single(result.Value.ForecastPoints);
        Assert.Equal(140m, result.Value.FinalProjectedValue);
    }

    [Fact]
    public async Task GetForecastByIdQueryHandler_ShouldReturnNotFound_WhenForecastDoesNotExist()
    {
        // Arrange
        var mockStore = new Mock<IForecastStore>();
        mockStore.Setup(s => s.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Forecast?)null);

        var handler = new GetForecastByIdQueryHandler(mockStore.Object);
        var query = new GetForecastByIdQuery(Guid.NewGuid(), Guid.NewGuid());

        // Act
        var result = await handler.HandleAsync(query);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(AiErrors.ForecastNotFound.Code, result.Error.Code);
    }

    [Fact]
    public async Task DeleteForecastCommandHandler_ShouldDelete_WhenForecastExists()
    {
        // Arrange
        var workspaceId = Guid.NewGuid();
        var forecastId = Guid.NewGuid();
        var domainForecast = new Forecast(
            workspaceId: workspaceId,
            createdByUserId: null,
            dataSourceId: null,
            metricName: "Churn",
            forecastPeriod: "Q1",
            historicalPeriod: "Q4",
            method: ForecastMethod.MovingAverage,
            confidenceInterval: 0.95,
            historicalPoints: new List<HistoricalDataPoint> { new("P1", 10m), new("P2", 12m) },
            forecastPoints: new List<ForecastPredictionPoint> { new("P3", 11m, 9m, 13m) },
            assumptions: new List<ForecastAssumption>(),
            projectedGrowthRate: 0m,
            finalProjectedValue: 11m);

        var mockStore = new Mock<IForecastStore>();
        mockStore.Setup(s => s.GetByIdAsync(forecastId, workspaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(domainForecast);

        var handler = new DeleteForecastCommandHandler(mockStore.Object);
        var command = new DeleteForecastCommand(workspaceId, forecastId);

        // Act
        var result = await handler.HandleAsync(command);

        // Assert
        Assert.True(result.IsSuccess);
        mockStore.Verify(s => s.DeleteAsync(domainForecast, It.IsAny<CancellationToken>()), Times.Once);
    }
}
