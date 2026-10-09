using System.Text.Json;
using FluentAssertions;
using IntelliQueryAI.AI.Agents;
using IntelliQueryAI.AI.Coordinator;
using IntelliQueryAI.Application.Features.Dashboards.Commands;
using IntelliQueryAI.Application.Features.Dashboards.Dtos;
using IntelliQueryAI.Application.Features.Dashboards.Queries;
using IntelliQueryAI.Domain.Entities.Dashboards;
using IntelliQueryAI.Domain.Errors;
using IntelliQueryAI.Domain.Persistence;
using IntelliQueryAI.Domain.Services;
using IntelliQueryAI.SharedKernel.Results;
using Moq;
using Xunit;

namespace IntelliQueryAI.Application.Tests;

public sealed class CustomDashboardTests
{
    private readonly Mock<IDashboardCoordinator> _dashboardCoordinatorMock = new();
    private readonly Mock<ICustomDashboardStore> _dashboardStoreMock = new();
    private readonly Mock<ITenantContext> _tenantContextMock = new();

    [Fact]
    public async Task GenerateDashboardWithAi_WhenValid_ReturnsParsedDashboardDto()
    {
        // Arrange
        var workspaceId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        _tenantContextMock.Setup(t => t.WorkspaceId).Returns(workspaceId);

        var agentResult = new DashboardAgentResult(
            Title: "Executive Revenue Overview",
            Description: "Quarterly revenue, MRR growth, and churn breakdown",
            DateRange: "Last 30 Days",
            Widgets: new[]
            {
                new DashboardAgentWidget(
                    Title: "Total MRR",
                    WidgetType: "metric",
                    Metric: "Monthly Recurring Revenue",
                    Dimension: null,
                    Aggregation: "SUM",
                    SqlQuery: "SELECT SUM(amount) AS mrr FROM subscriptions",
                    DateRange: "Last 30 Days",
                    ColSpan: 1,
                    RowSpan: 1),
                new DashboardAgentWidget(
                    Title: "Revenue by Region",
                    WidgetType: "barChart",
                    Metric: "Revenue",
                    Dimension: "Region",
                    Aggregation: "SUM",
                    SqlQuery: "SELECT region, SUM(amount) AS total FROM subscriptions GROUP BY region",
                    DateRange: "Last 30 Days",
                    ColSpan: 2,
                    RowSpan: 1)
            });

        _dashboardCoordinatorMock.Setup(c => c.GenerateDashboardDefinitionAsync(
            It.IsAny<GenerateDashboardPromptRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(agentResult));

        var handler = new GenerateDashboardWithAiCommandHandler(
            _dashboardCoordinatorMock.Object,
            _tenantContextMock.Object);

        var command = new GenerateDashboardWithAiCommand(
            workspaceId,
            userId,
            "Create an executive revenue overview");

        // Act
        var result = await handler.HandleAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Title.Should().Be("Executive Revenue Overview");
        result.Value.Widgets.Should().HaveCount(2);
        result.Value.Widgets[0].WidgetType.Should().Be("metric");
        result.Value.Widgets[1].WidgetType.Should().Be("barChart");
    }

    [Fact]
    public async Task CreateCustomDashboard_WhenUnsupportedWidgetType_ReturnsFailure()
    {
        // Arrange
        var workspaceId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        _tenantContextMock.Setup(t => t.WorkspaceId).Returns(workspaceId);

        var handler = new CreateCustomDashboardCommandHandler(
            _dashboardStoreMock.Object,
            _tenantContextMock.Object);

        var invalidWidget = new DashboardWidgetDto(
            Id: Guid.NewGuid().ToString(),
            Title: "Dangerous Script",
            WidgetType: "rawJavaScriptExecution",
            Metric: "InjectedCode");

        var command = new CreateCustomDashboardCommand(
            workspaceId,
            userId,
            "Hacked Dashboard",
            Widgets: new[] { invalidWidget });

        // Act
        var result = await handler.HandleAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(AiErrors.UnsupportedWidgetType(invalidWidget.WidgetType).Code);
    }

    [Fact]
    public async Task CreateCustomDashboard_WhenValidWidgets_SavesAndReturnsDto()
    {
        // Arrange
        var workspaceId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        _tenantContextMock.Setup(t => t.WorkspaceId).Returns(workspaceId);

        var handler = new CreateCustomDashboardCommandHandler(
            _dashboardStoreMock.Object,
            _tenantContextMock.Object);

        var validWidget = new DashboardWidgetDto(
            Id: Guid.NewGuid().ToString(),
            Title: "Sales Trend",
            WidgetType: "lineChart",
            Metric: "Revenue",
            Dimension: "Date",
            Aggregation: "SUM");

        var command = new CreateCustomDashboardCommand(
            workspaceId,
            userId,
            "Sales Dashboard",
            Description: "Track weekly sales velocity",
            Widgets: new[] { validWidget });

        // Act
        var result = await handler.HandleAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Title.Should().Be("Sales Dashboard");
        result.Value.Widgets.Should().HaveCount(1);
        result.Value.Widgets[0].WidgetType.Should().Be("lineChart");
        _dashboardStoreMock.Verify(s => s.AddAsync(It.IsAny<CustomDashboard>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetCustomDashboardById_WhenNotFound_ReturnsFailure()
    {
        // Arrange
        var workspaceId = Guid.NewGuid();
        var dashboardId = Guid.NewGuid();
        _tenantContextMock.Setup(t => t.WorkspaceId).Returns(workspaceId);

        _dashboardStoreMock.Setup(s => s.GetByIdAsync(dashboardId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CustomDashboard?)null);

        var handler = new GetCustomDashboardByIdQueryHandler(
            _dashboardStoreMock.Object,
            _dashboardCoordinatorMock.Object,
            _tenantContextMock.Object);

        var query = new GetCustomDashboardByIdQuery(workspaceId, dashboardId);

        // Act
        var result = await handler.HandleAsync(query);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(AiErrors.DashboardNotFound.Code);
    }

    [Fact]
    public async Task DuplicateCustomDashboard_WhenOriginalExists_CreatesDuplicate()
    {
        // Arrange
        var workspaceId = Guid.NewGuid();
        var originalId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        _tenantContextMock.Setup(t => t.WorkspaceId).Returns(workspaceId);

        var original = CustomDashboard.Create(
            workspaceId,
            userId,
            "Operations Overview",
            "Monitors system uptime and latency",
            "Show uptime",
            "[]",
            "Last 7 Days",
            isDefault: false,
            isShared: true);

        _dashboardStoreMock.Setup(s => s.GetByIdAsync(originalId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(original);

        var handler = new DuplicateCustomDashboardCommandHandler(
            _dashboardStoreMock.Object,
            _tenantContextMock.Object);

        var command = new DuplicateCustomDashboardCommand(workspaceId, originalId, userId);

        // Act
        var result = await handler.HandleAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Title.Should().Be("Copy of Operations Overview");
        _dashboardStoreMock.Verify(s => s.AddAsync(It.Is<CustomDashboard>(d => d.Title == "Copy of Operations Overview"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task WidgetValidator_StrictlyEnforcesWhitelistedTypes()
    {
        // Assert valid types
        DashboardWidgetValidator.IsValidWidgetType("metric").Should().BeTrue();
        DashboardWidgetValidator.IsValidWidgetType("lineChart").Should().BeTrue();
        DashboardWidgetValidator.IsValidWidgetType("barChart").Should().BeTrue();
        DashboardWidgetValidator.IsValidWidgetType("areaChart").Should().BeTrue();
        DashboardWidgetValidator.IsValidWidgetType("pieChart").Should().BeTrue();
        DashboardWidgetValidator.IsValidWidgetType("table").Should().BeTrue();

        // Case insensitivity & formatting
        DashboardWidgetValidator.NormalizeWidgetType("LINE_CHART").Should().Be("lineChart");
        DashboardWidgetValidator.NormalizeWidgetType("PIE").Should().Be("pieChart");
        DashboardWidgetValidator.NormalizeWidgetType("KPI").Should().Be("metric");

        // Assert invalid types are rejected
        DashboardWidgetValidator.IsValidWidgetType("script").Should().BeFalse();
        DashboardWidgetValidator.IsValidWidgetType("eval").Should().BeFalse();
        DashboardWidgetValidator.IsValidWidgetType("iframe").Should().BeFalse();
        DashboardWidgetValidator.IsValidWidgetType("react_component").Should().BeFalse();
    }
}
