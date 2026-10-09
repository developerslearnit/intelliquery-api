using FluentAssertions;
using IntelliQueryAI.AI.Agents;
using IntelliQueryAI.AI.Coordinator;
using IntelliQueryAI.Application.Abstractions.Messaging;
using IntelliQueryAI.Application.Features.Dashboard.Commands.GenerateMorningExecutiveBrief;
using IntelliQueryAI.Application.Features.Dashboard.Dtos;
using IntelliQueryAI.Application.Features.Dashboard.Queries.GetMorningExecutiveBrief;
using IntelliQueryAI.Domain.Entities.Alerts;
using IntelliQueryAI.Domain.Entities.Ai;
using IntelliQueryAI.Domain.Entities.BusinessKnowledge;
using IntelliQueryAI.Domain.Entities.Executive;
using IntelliQueryAI.Domain.Entities.Monitoring;
using IntelliQueryAI.Domain.Entities.Workspaces;
using IntelliQueryAI.Domain.Enums;
using IntelliQueryAI.Domain.Errors;
using IntelliQueryAI.Domain.Persistence;
using IntelliQueryAI.Domain.Services;
using IntelliQueryAI.Domain.ValueObjects;
using IntelliQueryAI.SharedKernel.Results;
using Moq;
using Xunit;
using Monitor = IntelliQueryAI.Domain.Entities.Monitoring.Monitor;

namespace IntelliQueryAI.Application.Tests;

public sealed class MorningExecutiveBriefTests
{
    private readonly Mock<IMetricStore> _metricStoreMock = new();
    private readonly Mock<IMonitorStore> _monitorStoreMock = new();
    private readonly Mock<IAlertStore> _alertStoreMock = new();
    private readonly Mock<IExecutiveReportStore> _executiveReportStoreMock = new();
    private readonly Mock<IQueryHistoryStore> _queryHistoryStoreMock = new();
    private readonly Mock<IAnomalyDetectionService> _anomalyServiceMock = new();
    private readonly Mock<ITenantContext> _tenantContextMock = new();
    private readonly Mock<IWorkspaceStore> _workspaceStoreMock = new();
    private readonly Mock<IExecutiveCoordinator> _executiveCoordinatorMock = new();
    private readonly Mock<IDispatcher> _dispatcherMock = new();

    private readonly GetMorningExecutiveBriefQueryHandler _queryHandler;

    public MorningExecutiveBriefTests()
    {
        _queryHandler = new GetMorningExecutiveBriefQueryHandler(
            _metricStoreMock.Object,
            _monitorStoreMock.Object,
            _alertStoreMock.Object,
            _executiveReportStoreMock.Object,
            _queryHistoryStoreMock.Object,
            _anomalyServiceMock.Object,
            _tenantContextMock.Object,
            _workspaceStoreMock.Object);
    }

    [Fact]
    public async Task GetMorningExecutiveBrief_WhenValidWorkspace_ReturnsDynamicMetricsAndPrioritizedFindings()
    {
        // Arrange
        var workspaceId = Guid.NewGuid();
        var workspace = Workspace.Create("Acme Corp", TenantSlug.Create("acme-corp").Value, SubscriptionPlan.Pro, Guid.NewGuid()).Value;
        _workspaceStoreMock.Setup(w => w.GetByIdAsync(workspaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(workspace);
        _tenantContextMock.Setup(t => t.WorkspaceId).Returns(workspaceId);

        var semanticMetric = MetricDefinition.Create(
            workspaceId,
            "Monthly Recurring Revenue",
            "Tracks active monthly recurring revenue",
            "SUM(mrr)",
            "USD",
            null,
            null,
            "subscriptions",
            "mrr",
            "SUM",
            "HigherIsBetter");

        _metricStoreMock.Setup(s => s.GetByWorkspaceIdAsync(workspaceId, It.IsAny<string?>(), true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MetricDefinition> { semanticMetric });

        var monitor = Monitor.Create(workspaceId, Guid.NewGuid(), "Revenue Health Monitor", "Audits MRR", MonitorType.Revenue, ScheduleType.Daily, null, "MRR");
        _monitorStoreMock.Setup(m => m.GetByWorkspaceIdAsync(workspaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Monitor> { monitor });

        _monitorStoreMock.Setup(m => m.GetRecentExecutionsByWorkspaceIdAsync(workspaceId, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MonitorExecution>());

        _alertStoreMock.Setup(a => a.GetByWorkspaceIdAsync(workspaceId, null, null, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Alert>());

        _queryHistoryStoreMock.Setup(q => q.GetByWorkspaceIdAsync(workspaceId, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<QueryHistory>());

        _executiveReportStoreMock.Setup(r => r.GetByWorkspaceIdAsync(workspaceId, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ExecutiveReport>());
        _executiveReportStoreMock.Setup(r => r.GetLatestByWorkspaceIdAsync(workspaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExecutiveReport?)null);

        var query = new GetMorningExecutiveBriefQuery(workspaceId, "Since yesterday", "Sarah");

        // Act
        var result = await _queryHandler.HandleAsync(query);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Greeting.Should().Contain("Sarah");
        result.Value.Period.Should().Be("Since yesterday");
        result.Value.Headline.Should().Contain("since yesterday");
        result.Value.Metrics.Should().NotBeEmpty();
        result.Value.RankedFindings.Should().NotBeEmpty();
        result.Value.EmergingRisks.Should().NotBeEmpty();
        result.Value.Opportunities.Should().NotBeEmpty();
        result.Value.Recommendations.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetMorningExecutiveBrief_WhenPeriodIsThisWeek_NormalizesPeriodAndCalculatesHorizon()
    {
        // Arrange
        var workspaceId = Guid.NewGuid();
        _tenantContextMock.Setup(t => t.WorkspaceId).Returns(workspaceId);

        _metricStoreMock.Setup(s => s.GetByWorkspaceIdAsync(workspaceId, It.IsAny<string?>(), true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MetricDefinition>());
        _monitorStoreMock.Setup(m => m.GetByWorkspaceIdAsync(workspaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Monitor>());
        _monitorStoreMock.Setup(m => m.GetRecentExecutionsByWorkspaceIdAsync(workspaceId, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MonitorExecution>());
        _alertStoreMock.Setup(a => a.GetByWorkspaceIdAsync(workspaceId, null, null, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Alert>());
        _queryHistoryStoreMock.Setup(q => q.GetByWorkspaceIdAsync(workspaceId, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<QueryHistory>());
        _executiveReportStoreMock.Setup(r => r.GetByWorkspaceIdAsync(workspaceId, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ExecutiveReport>());

        var query = new GetMorningExecutiveBriefQuery(workspaceId, "This week", "Alex");

        // Act
        var result = await _queryHandler.HandleAsync(query);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Period.Should().Be("This week");
        result.Value.Headline.Should().Contain("this week");
    }

    [Fact]
    public async Task GetMorningExecutiveBrief_WhenCriticalAlertsExist_RanksThemFirstInFindings()
    {
        // Arrange
        var workspaceId = Guid.NewGuid();
        _tenantContextMock.Setup(t => t.WorkspaceId).Returns(workspaceId);

        var monitor = Monitor.Create(workspaceId, Guid.NewGuid(), "Latency Monitor", "Audits latency", MonitorType.Custom, ScheduleType.Hourly);
        _monitorStoreMock.Setup(m => m.GetByWorkspaceIdAsync(workspaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Monitor> { monitor });
        _monitorStoreMock.Setup(m => m.GetRecentExecutionsByWorkspaceIdAsync(workspaceId, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MonitorExecution>());

        var criticalAlert = Alert.Create(
            workspaceId,
            monitor.Id,
            AlertType.ThresholdExceeded,
            AlertSeverity.Critical,
            "Critical Latency Breach",
            "Query latency crossed 3500ms threshold",
            3500m,
            2000m);

        _alertStoreMock.Setup(a => a.GetByWorkspaceIdAsync(workspaceId, null, null, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Alert> { criticalAlert });

        _metricStoreMock.Setup(s => s.GetByWorkspaceIdAsync(workspaceId, It.IsAny<string?>(), true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MetricDefinition>());
        _queryHistoryStoreMock.Setup(q => q.GetByWorkspaceIdAsync(workspaceId, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<QueryHistory>());
        _executiveReportStoreMock.Setup(r => r.GetByWorkspaceIdAsync(workspaceId, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ExecutiveReport>());

        var query = new GetMorningExecutiveBriefQuery(workspaceId, "Since yesterday", "Mark");

        // Act
        var result = await _queryHandler.HandleAsync(query);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.CriticalAlerts.Should().HaveCount(1);
        result.Value.CriticalAlerts[0].Severity.Should().Be("Critical");
        result.Value.RankedFindings[0].Impact.Should().Be("High");
        result.Value.RankedFindings[0].Title.Should().Contain("Critical Latency Breach");
    }

    [Fact]
    public async Task GetMorningExecutiveBrief_WhenTenantMismatch_ReturnsUnauthorizedFailure()
    {
        // Arrange
        var workspaceId = Guid.NewGuid();
        var unauthorizedWorkspaceId = Guid.NewGuid();
        _tenantContextMock.Setup(t => t.WorkspaceId).Returns(unauthorizedWorkspaceId);

        var query = new GetMorningExecutiveBriefQuery(workspaceId, "Since yesterday");

        // Act
        var result = await _queryHandler.HandleAsync(query);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(AiErrors.ContextTenantMismatch);
    }

    [Fact]
    public async Task GenerateMorningExecutiveBrief_CallsCoordinatorAndReturnsRefreshedBrief()
    {
        // Arrange
        var workspaceId = Guid.NewGuid();
        _tenantContextMock.Setup(t => t.WorkspaceId).Returns(workspaceId);

        _metricStoreMock.Setup(s => s.GetByWorkspaceIdAsync(workspaceId, It.IsAny<string?>(), true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MetricDefinition>());
        _monitorStoreMock.Setup(m => m.GetByWorkspaceIdAsync(workspaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Monitor>());
        _monitorStoreMock.Setup(m => m.GetRecentExecutionsByWorkspaceIdAsync(workspaceId, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MonitorExecution>());
        _alertStoreMock.Setup(a => a.GetByWorkspaceIdAsync(workspaceId, true, null, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Alert>());
        _queryHistoryStoreMock.Setup(q => q.GetByWorkspaceIdAsync(workspaceId, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<QueryHistory>());

        var coordResponse = new GenerateExecutiveReportResponse(
            "Acme Executive Briefing - Since yesterday",
            "Since yesterday",
            "Acme summary nominal.",
            new List<string> { "Query volumes nominal" },
            new List<string> { "None" },
            new List<string> { "Expansion" },
            new List<ActionableRecommendation>(),
            120,
            45);

        _executiveCoordinatorMock.Setup(c => c.GenerateExecutiveBriefingAsync(It.IsAny<GenerateExecutiveReportRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(coordResponse));

        var expectedDto = new MorningExecutiveBriefDto(
            Guid.NewGuid(),
            "Good morning, Executive",
            "Here's what changed in your business since yesterday.",
            "Since yesterday",
            "Summary narrative",
            DateTime.UtcNow,
            Array.Empty<ExecutiveBriefMetricDto>(),
            Array.Empty<ExecutiveBriefAlertDto>(),
            Array.Empty<ExecutiveBriefAnomalyDto>(),
            Array.Empty<ExecutiveBriefFindingDto>(),
            Array.Empty<ExecutiveBriefRiskDto>(),
            Array.Empty<ExecutiveBriefOpportunityDto>(),
            Array.Empty<ActionableRecommendation>());

        _dispatcherMock.Setup(d => d.QueryAsync(It.IsAny<GetMorningExecutiveBriefQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(expectedDto));

        var commandHandler = new GenerateMorningExecutiveBriefCommandHandler(
            _executiveCoordinatorMock.Object,
            _executiveReportStoreMock.Object,
            _metricStoreMock.Object,
            _monitorStoreMock.Object,
            _alertStoreMock.Object,
            _queryHistoryStoreMock.Object,
            _tenantContextMock.Object,
            _dispatcherMock.Object,
            _workspaceStoreMock.Object);

        var command = new GenerateMorningExecutiveBriefCommand(workspaceId, "Since yesterday", "Executive");

        // Act
        var result = await commandHandler.HandleAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Period.Should().Be("Since yesterday");
        _executiveReportStoreMock.Verify(r => r.AddAsync(It.IsAny<ExecutiveReport>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
