using System.Text.Json;
using FluentAssertions;
using IntelliQueryAI.AI.Agents;
using IntelliQueryAI.AI.Coordinator;
using IntelliQueryAI.Application.Features.Scenarios.Commands;
using IntelliQueryAI.Application.Features.Scenarios.Dtos;
using IntelliQueryAI.Application.Features.Scenarios.Queries;
using IntelliQueryAI.Domain.Entities.Audit;
using IntelliQueryAI.Domain.Entities.Scenarios;
using IntelliQueryAI.Domain.Errors;
using IntelliQueryAI.Domain.Persistence;
using IntelliQueryAI.Domain.Services;
using IntelliQueryAI.SharedKernel.Results;
using Moq;
using Xunit;

namespace IntelliQueryAI.Application.Tests;

public sealed class ScenarioTests
{
    private readonly Mock<IScenarioCoordinator> _coordinatorMock = new();
    private readonly Mock<IScenarioStore> _scenarioStoreMock = new();
    private readonly Mock<IAuditLogStore> _auditStoreMock = new();
    private readonly Mock<ITenantContext> _tenantContextMock = new();
    private readonly IScenarioSimulationEngine _simulationEngine = new ScenarioSimulationEngine();

    [Fact]
    public void ScenarioSimulationEngine_CalculatesExpectedBestAndWorstCasesDeterministically()
    {
        // Arrange
        decimal baseline = 100_000m;
        var variables = new[]
        {
            new ScenarioVariable("Pricing Increase", "%", 100m, 10m, Weight: 1.0),
            new ScenarioVariable("Customer Churn Acceleration", "%", 5m, 2m, Weight: -1.0)
        };

        // Act
        var result = _simulationEngine.CalculateSimulation(baseline, "Next 12 Months", variables);

        // Assert
        // Net change: +10% - 2% = +8%
        result.BaselineValue.Should().Be(100_000m);
        result.ExpectedValue.Should().Be(108_000m);
        result.ExpectedDeltaValue.Should().Be(8_000m);
        result.ExpectedDeltaPercentage.Should().Be(8.00m);

        // Best case should be higher than expected case
        result.BestCaseValue.Should().BeGreaterThan(result.ExpectedValue);

        // Worst case should be lower than baseline (stress test)
        result.WorstCaseValue.Should().BeLessThan(result.ExpectedValue);

        // Trajectory should contain 12 months
        result.Trajectory.Should().HaveCount(12);
        result.Trajectory[0].Baseline.Should().Be(100_000m);

        // Confidence should be within safe statistical bounds
        result.ConfidenceScore.Should().BeInRange(0.55, 0.95);
    }

    [Theory]
    [InlineData("Next Quarter", 3)]
    [InlineData("Next 6 Months", 6)]
    [InlineData("Next 12 Months", 12)]
    public void ScenarioSimulationEngine_GeneratesCorrectMonthlyHorizonPoints(string horizon, int expectedMonths)
    {
        // Arrange
        decimal baseline = 50_000m;
        var variables = new[]
        {
            new ScenarioVariable("Sales Volume", "units", 1000m, 5m, Weight: 1.0)
        };

        // Act
        var result = _simulationEngine.CalculateSimulation(baseline, horizon, variables);

        // Assert
        result.Trajectory.Should().HaveCount(expectedMonths);
    }

    [Fact]
    public async Task CreateScenario_WhenValid_SimulatesPersistsAndAudits()
    {
        // Arrange
        var workspaceId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        _tenantContextMock.Setup(t => t.WorkspaceId).Returns(workspaceId);

        var variableDtos = new[]
        {
            new ScenarioVariableDto("Revenue Growth", "%", 500_000m, 12m, Weight: 1.0)
        };

        var simResult = _simulationEngine.CalculateSimulation(500_000m, "Next 12 Months",
            new[] { new ScenarioVariable("Revenue Growth", "%", 500_000m, 12m, Weight: 1.0) });

        var narrative = new ScenarioAnalysisNarrative(
            Summary: "Robust growth scenario driven by enterprise expansion.",
            KeyDrivers: new[] { "Pricing resilience" },
            RiskFactors: new[] { "Churn exposure" },
            UpsideCatalysts: new[] { "Upsell momentum" },
            StrategicRecommendations: new[] { "Expand sales coverage" });

        var coordResult = new CalculateScenarioResult(
            simResult,
            narrative,
            JsonSerializer.Serialize(narrative),
            DurationMs: 120);

        _coordinatorMock.Setup(c => c.CalculateAndAnalyzeAsync(It.IsAny<CalculateScenarioRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(coordResult));

        var command = new CreateScenarioCommand(
            WorkspaceId: workspaceId,
            UserId: userId,
            Title: "FY27 Aggressive Growth",
            Description: "Simulating 12% revenue growth",
            TargetMetric: "ARR",
            BaselineValue: 500_000m,
            TimeHorizon: "Next 12 Months",
            Variables: variableDtos);

        var handler = new CreateScenarioCommandHandler(
            _coordinatorMock.Object,
            _scenarioStoreMock.Object,
            _auditStoreMock.Object,
            _tenantContextMock.Object);

        // Act
        var result = await handler.HandleAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Title.Should().Be("FY27 Aggressive Growth");
        result.Value.TargetMetric.Should().Be("ARR");
        result.Value.Projections.Should().HaveCount(4);

        _scenarioStoreMock.Verify(s => s.AddAsync(It.Is<Scenario>(sc =>
            sc.WorkspaceId == workspaceId &&
            sc.Title == "FY27 Aggressive Growth" &&
            sc.TargetMetric == "ARR"), It.IsAny<CancellationToken>()), Times.Once);

        _auditStoreMock.Verify(a => a.AddAsync(It.Is<AuditLog>(l =>
            l.WorkspaceId == workspaceId &&
            l.Action == "Scenario.Created"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RerunScenarioSimulation_WhenTenantMismatch_ReturnsFailure()
    {
        // Arrange
        var workspaceId = Guid.NewGuid();
        var otherWorkspaceId = Guid.NewGuid();
        var scenarioId = Guid.NewGuid();
        _tenantContextMock.Setup(t => t.WorkspaceId).Returns(workspaceId);

        var scenario = Scenario.Create(
            otherWorkspaceId,
            Guid.NewGuid(),
            "Foreign Scenario",
            null,
            "Revenue",
            100_000m,
            "Next 12 Months",
            Array.Empty<ScenarioVariable>(),
            Array.Empty<ScenarioProjection>());

        _scenarioStoreMock.Setup(s => s.GetByIdAsync(scenarioId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(scenario);

        var handler = new RerunScenarioSimulationCommandHandler(
            _coordinatorMock.Object,
            _scenarioStoreMock.Object,
            _auditStoreMock.Object,
            _tenantContextMock.Object);

        // Act
        var result = await handler.HandleAsync(new RerunScenarioSimulationCommand(workspaceId, Guid.NewGuid(), scenarioId));

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(AiErrors.ScenarioTenantMismatch.Code);
    }

    [Fact]
    public async Task DeleteScenario_WhenFound_RemovesFromStoreAndAudits()
    {
        // Arrange
        var workspaceId = Guid.NewGuid();
        var scenarioId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        _tenantContextMock.Setup(t => t.WorkspaceId).Returns(workspaceId);

        var scenario = Scenario.Create(
            workspaceId,
            userId,
            "Test Scenario",
            null,
            "Revenue",
            100_000m,
            "Next 12 Months",
            Array.Empty<ScenarioVariable>(),
            Array.Empty<ScenarioProjection>());

        _scenarioStoreMock.Setup(s => s.GetByIdAsync(scenarioId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(scenario);

        var handler = new DeleteScenarioCommandHandler(
            _scenarioStoreMock.Object,
            _auditStoreMock.Object,
            _tenantContextMock.Object);

        // Act
        var result = await handler.HandleAsync(new DeleteScenarioCommand(workspaceId, userId, scenarioId));

        // Assert
        result.IsSuccess.Should().BeTrue();
        _scenarioStoreMock.Verify(s => s.DeleteAsync(scenario, It.IsAny<CancellationToken>()), Times.Once);
        _auditStoreMock.Verify(a => a.AddAsync(It.Is<AuditLog>(l =>
            l.Action == "Scenario.Deleted"), It.IsAny<CancellationToken>()), Times.Once);
    }
}
