using FluentAssertions;
using IntelliQueryAI.AI.Agents;
using IntelliQueryAI.AI.Coordinator;
using IntelliQueryAI.Application.Features.Ai.Commands.ExecuteChatQuery;
using IntelliQueryAI.Application.Features.Ai.Dtos;
using IntelliQueryAI.Application.Features.Ai.Queries.ResolveAiContext;
using IntelliQueryAI.Domain.Entities.Alerts;
using IntelliQueryAI.Domain.Entities.BusinessKnowledge;
using IntelliQueryAI.Domain.Entities.Executive;
using IntelliQueryAI.Domain.Entities.Workspaces;
using IntelliQueryAI.Domain.Enums;
using IntelliQueryAI.Domain.Errors;
using IntelliQueryAI.Domain.Persistence;
using IntelliQueryAI.Domain.Services;
using IntelliQueryAI.Domain.ValueObjects;
using IntelliQueryAI.SharedKernel.Results;
using Moq;
using Xunit;

namespace IntelliQueryAI.Application.Tests;

public sealed class ContextualAiTests
{
    private readonly Mock<ITenantContext> _tenantContextMock = new();
    private readonly Mock<IWorkspaceStore> _workspaceStoreMock = new();
    private readonly Mock<IAlertStore> _alertStoreMock = new();
    private readonly Mock<IMonitorStore> _monitorStoreMock = new();
    private readonly Mock<IExecutiveReportStore> _executiveReportStoreMock = new();
    private readonly Mock<IInvestigationReportStore> _investigationReportStoreMock = new();
    private readonly Mock<IDataSourceStore> _dataSourceStoreMock = new();
    private readonly Mock<IMetricStore> _metricStoreMock = new();
    private readonly Mock<ISavedReportStore> _savedReportStoreMock = new();

    private readonly ResolveAiContextQueryHandler _handler;

    public ContextualAiTests()
    {
        _handler = new ResolveAiContextQueryHandler(
            _tenantContextMock.Object,
            _workspaceStoreMock.Object,
            _alertStoreMock.Object,
            _monitorStoreMock.Object,
            _executiveReportStoreMock.Object,
            _investigationReportStoreMock.Object,
            _dataSourceStoreMock.Object,
            _metricStoreMock.Object,
            _savedReportStoreMock.Object);
    }

    [Fact]
    public async Task ResolveAiContext_WhenKpi_ReturnsIntelligentPromptAndFollowUps()
    {
        // Arrange
        var workspaceId = Guid.NewGuid();
        var workspace = Workspace.Create("Acme Analytics", TenantSlug.Create("acme-analytics").Value, SubscriptionPlan.Pro, Guid.NewGuid()).Value;
        _workspaceStoreMock.Setup(s => s.GetByIdAsync(workspaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(workspace);

        var metric = MetricDefinition.Create(
            workspaceId,
            "Gross Margin",
            "Percentage of revenue remaining after COGS",
            "SUM(Revenue - COGS) / SUM(Revenue) * 100",
            "USD");

        _metricStoreMock.Setup(s => s.GetByWorkspaceIdAsync(workspaceId, "Gross Margin", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MetricDefinition> { metric });

        var query = new ResolveAiContextQuery(
            WorkspaceId: workspaceId,
            ContextType: "kpi",
            Metric: "Gross Margin",
            DateRange: "Last 30 Days");

        // Act
        var result = await _handler.HandleAsync(query);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.IsValid.Should().BeTrue();
        result.Value.ContextType.Should().Be("kpi");
        result.Value.SuggestedPrompt.Should().Contain("Gross Margin");
        result.Value.SuggestedFollowUpQuestions.Should().HaveCountGreaterThanOrEqualTo(3);
        result.Value.EnrichedMetadata.Should().ContainKey("Formula");
        result.Value.EnrichedMetadata["Formula"].Should().Be("SUM(Revenue - COGS) / SUM(Revenue) * 100");
    }

    [Fact]
    public async Task ResolveAiContext_WhenAlert_ValidatesTenantAndReturnsAlertMetadata()
    {
        // Arrange
        var workspaceId = Guid.NewGuid();
        var workspace = Workspace.Create("Acme Analytics", TenantSlug.Create("acme-analytics").Value, SubscriptionPlan.Pro, Guid.NewGuid()).Value;
        _workspaceStoreMock.Setup(s => s.GetByIdAsync(workspaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(workspace);

        var alertId = Guid.NewGuid();
        var alert = Alert.Create(
            workspaceId,
            null,
            AlertType.ThresholdExceeded,
            AlertSeverity.Critical,
            "Payment Processing Latency Spike",
            "P99 latency breached 1,200ms threshold.",
            1240.50m,
            500.00m);

        _alertStoreMock.Setup(s => s.GetByIdAsync(alertId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(alert);

        var query = new ResolveAiContextQuery(
            WorkspaceId: workspaceId,
            ContextType: "alert",
            EntityId: alertId);

        // Act
        var result = await _handler.HandleAsync(query);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.EntityTitle.Should().Be("Payment Processing Latency Spike");
        result.Value.SuggestedPrompt.Should().Contain("Explain this alert");
        result.Value.EnrichedMetadata.Should().ContainKey("AlertSeverity");
        result.Value.EnrichedMetadata["AlertSeverity"].Should().Be(AlertSeverity.Critical.ToString());
        result.Value.EnrichedMetadata["MetricValue"].Should().Be("1240.50");
    }

    [Fact]
    public async Task ResolveAiContext_WhenEntityBelongsToDifferentWorkspace_FailsWithTenantMismatch()
    {
        // Arrange
        var userWorkspaceId = Guid.NewGuid();
        var otherWorkspaceId = Guid.NewGuid();

        var workspace = Workspace.Create("User Workspace", TenantSlug.Create("user-ws").Value, SubscriptionPlan.Pro, Guid.NewGuid()).Value;
        _workspaceStoreMock.Setup(s => s.GetByIdAsync(userWorkspaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(workspace);

        var alertId = Guid.NewGuid();
        var alertFromOtherWorkspace = Alert.Create(
            otherWorkspaceId,
            null,
            AlertType.ThresholdExceeded,
            AlertSeverity.Critical,
            "Competitor Alert",
            "Foreign tenant data");

        _alertStoreMock.Setup(s => s.GetByIdAsync(alertId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(alertFromOtherWorkspace);

        var query = new ResolveAiContextQuery(
            WorkspaceId: userWorkspaceId,
            ContextType: "alert",
            EntityId: alertId);

        // Act
        var result = await _handler.HandleAsync(query);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(AiErrors.ContextTenantMismatch.Code);
    }

    [Fact]
    public async Task ResolveAiContext_WhenEntityNotFound_FailsWithEntityNotFound()
    {
        // Arrange
        var workspaceId = Guid.NewGuid();
        var workspace = Workspace.Create("User Workspace", TenantSlug.Create("user-ws").Value, SubscriptionPlan.Pro, Guid.NewGuid()).Value;
        _workspaceStoreMock.Setup(s => s.GetByIdAsync(workspaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(workspace);

        var nonExistentAlertId = Guid.NewGuid();
        _alertStoreMock.Setup(s => s.GetByIdAsync(nonExistentAlertId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Alert?)null);

        var query = new ResolveAiContextQuery(
            WorkspaceId: workspaceId,
            ContextType: "alert",
            EntityId: nonExistentAlertId);

        // Act
        var result = await _handler.HandleAsync(query);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(AiErrors.ContextEntityNotFound("Alert", nonExistentAlertId).Code);
    }

    [Fact]
    public async Task ExecuteChatQuery_WithContext_PassesContextPromptToCoordinator()
    {
        // Arrange
        var workspaceId = Guid.NewGuid();
        var dsId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var coordinatorMock = new Mock<IAgentCoordinator>();
        var subStoreMock = new Mock<ISubscriptionStore>();
        var auditMock = new Mock<IAuditLogStore>();
        var uowMock = new Mock<IUnitOfWork>();

        CoordinatorQueryRequest? capturedRequest = null;
        coordinatorMock.Setup(c => c.ExecuteQueryPipelineAsync(It.IsAny<CoordinatorQueryRequest>(), It.IsAny<CancellationToken>()))
            .Callback<CoordinatorQueryRequest, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(Result.Success(new CoordinatorQueryResponse(
                Question: "Why did revenue drop?",
                GeneratedSql: "SELECT SUM(amount) FROM sales",
                Results: new ExecutionQueryResult(new[] { "sum" }, new List<IReadOnlyDictionary<string, object?>>(), 0, 15, true, null),
                Insights: new[] { "Variance detected." },
                RecommendedChart: new ChartConfiguration(ChartType.BarChart, "Revenue Breakdown", "period", new[] { "amount" }, "Chart", null),
                ConfidenceScore: 0.95,
                TotalDurationMs: 120,
                TotalTokensUsed: 450)));

        var commandHandler = new ExecuteChatQueryCommandHandler(
            coordinatorMock.Object,
            subStoreMock.Object,
            auditMock.Object,
            uowMock.Object);

        var contextDto = new AiContextDto(
            WorkspaceId: workspaceId,
            ContextType: "kpi",
            Metric: "Revenue",
            EntityTitle: "Monthly Recurring Revenue",
            DateRange: "Q3 2026",
            AdditionalMetadata: new Dictionary<string, string> { ["Deviation"] = "-15%" });

        var command = new ExecuteChatQueryCommand(
            WorkspaceId: workspaceId,
            DataSourceId: dsId,
            UserId: userId,
            Question: "Why did revenue drop?",
            Context: contextDto);

        // Act
        var result = await commandHandler.HandleAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        capturedRequest.Should().NotBeNull();
        capturedRequest!.ContextPrompt.Should().NotBeNull();
        capturedRequest.ContextPrompt.Should().Contain("[KPI] Monthly Recurring Revenue");
        capturedRequest.ContextPrompt.Should().Contain("Target Metric: Revenue");
        capturedRequest.ContextPrompt.Should().Contain("Deviation=-15%");
    }

    [Fact]
    public void AiContextDto_JsonDeserialization_HandlesNumericAndBooleanMetadata()
    {
        // Arrange
        const string json = """
        {
            "contextType": "dashboard",
            "entityId": "6ce7f9b9-d5c2-4d02-9d46-a0962f8ed486",
            "entityTitle": "AI Executive Dashboard",
            "dateRange": "Last 30 Days",
            "additionalMetadata": {
                "widgetsCount": 6,
                "isAutomated": true,
                "prompt": "Create revenue dashboard comparing transactions by bank"
            }
        }
        """;

        // Act
        var options = new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web);
        var result = System.Text.Json.JsonSerializer.Deserialize<AiContextDto>(json, options);

        // Assert
        result.Should().NotBeNull();
        result!.ContextType.Should().Be("dashboard");
        result.EntityId.Should().Be(Guid.Parse("6ce7f9b9-d5c2-4d02-9d46-a0962f8ed486"));
        result.WorkspaceId.Should().BeNull();
        result.AdditionalMetadata.Should().NotBeNull();
        result.AdditionalMetadata!["widgetsCount"].Should().Be("6");
        result.AdditionalMetadata["isAutomated"].Should().Be("true");
        result.AdditionalMetadata["prompt"].Should().Be("Create revenue dashboard comparing transactions by bank");
    }
}
