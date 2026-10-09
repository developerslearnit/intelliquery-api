using FluentAssertions;
using IntelliQueryAI.Application.Features.Alerts.Commands.AcknowledgeAlert;
using IntelliQueryAI.Application.Features.Dashboard.Queries.GetDashboardExecutiveSummary;
using IntelliQueryAI.Application.Features.Monitoring.Commands.CreateMonitor;
using IntelliQueryAI.Application.Features.Monitoring.Dtos;
using IntelliQueryAI.Domain.Entities.Alerts;
using IntelliQueryAI.Domain.Entities.Audit;
using IntelliQueryAI.Domain.Entities.DataSources;
using IntelliQueryAI.Domain.Entities.Executive;
using IntelliQueryAI.Domain.Entities.Monitoring;
using IntelliQueryAI.Domain.Enums;
using IntelliQueryAI.Domain.Persistence;
using Moq;
using Xunit;
using Monitor = IntelliQueryAI.Domain.Entities.Monitoring.Monitor;

namespace IntelliQueryAI.Application.Tests;

public sealed class MonitoringAndAlertsCommandHandlerTests
{
    [Fact]
    public async Task CreateMonitorCommandHandler_WhenValid_ShouldPersistMonitorAndAudit()
    {
        // Arrange
        var workspaceId = Guid.NewGuid();
        var dataSourceId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var mockMonitorStore = new Mock<IMonitorStore>();
        var mockDataSourceStore = new Mock<IDataSourceStore>();
        var mockAuditStore = new Mock<IAuditLogStore>();
        var mockUnitOfWork = new Mock<IUnitOfWork>();

        var fakeDataSource = DataSource.Create(
            workspaceId,
            "Production Postgres",
            DatabaseType.PostgreSQL,
            "encrypted_connection_string").Value;

        mockDataSourceStore
            .Setup(x => x.GetByIdAsync(dataSourceId, It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult<DataSource?>(fakeDataSource));

        var handler = new CreateMonitorCommandHandler(
            mockMonitorStore.Object,
            mockDataSourceStore.Object,
            mockAuditStore.Object,
            mockUnitOfWork.Object);

        var command = new CreateMonitorCommand(
            workspaceId,
            userId,
            dataSourceId,
            "Failed Payment Monitor",
            "Monitors failed transaction events",
            MonitorType.FailedPayment,
            ScheduleType.Daily,
            "SELECT COUNT(*) FROM payments WHERE status = 'failed'",
            "FailedPayments",
            new List<CreateMonitorRuleRequest>
            {
                new(RuleType.Threshold, 10m, ComparisonOperator.GreaterThanOrEqual, "1d")
            });

        // Act
        var result = await handler.HandleAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Failed Payment Monitor");
        result.Value.MonitorType.Should().Be("FailedPayment");
        result.Value.Rules.Should().HaveCount(1);

        mockMonitorStore.Verify(x => x.AddAsync(It.IsAny<Monitor>(), It.IsAny<CancellationToken>()), Times.Once);
        mockAuditStore.Verify(x => x.AddAsync(It.IsAny<AuditLog>(), It.IsAny<CancellationToken>()), Times.Once);
        mockUnitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AcknowledgeAlertCommandHandler_WhenAlertExists_ShouldMarkAcknowledged()
    {
        // Arrange
        var workspaceId = Guid.NewGuid();
        var alertId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var mockAlertStore = new Mock<IAlertStore>();
        var mockAuditStore = new Mock<IAuditLogStore>();
        var mockUnitOfWork = new Mock<IUnitOfWork>();

        var alert = Alert.Create(
            workspaceId,
            null,
            AlertType.ThresholdExceeded,
            AlertSeverity.Critical,
            "Revenue dropped 20%",
            "Observed revenue below baseline.");

        mockAlertStore
            .Setup(x => x.GetByIdAsync(alertId, It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult<Alert?>(alert));

        var handler = new AcknowledgeAlertCommandHandler(
            mockAlertStore.Object,
            mockAuditStore.Object,
            mockUnitOfWork.Object);

        var command = new AcknowledgeAlertCommand(workspaceId, alertId, userId);

        // Act
        var result = await handler.HandleAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.IsAcknowledged.Should().BeTrue();
        alert.IsAcknowledged.Should().BeTrue();
        alert.AcknowledgedByUserId.Should().Be(userId);

        mockAlertStore.Verify(x => x.UpdateAsync(alert, It.IsAny<CancellationToken>()), Times.Once);
        mockUnitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetDashboardExecutiveSummaryQueryHandler_WhenReportExists_ShouldReturnMappedDto()
    {
        // Arrange
        var workspaceId = Guid.NewGuid();
        var mockExecStore = new Mock<IExecutiveReportStore>();

        var report = ExecutiveReport.Create(
            workspaceId,
            "Monthly Executive Intelligence Briefing",
            "October 2026",
            "Business operations are expanding healthily.",
            "[\"Query throughput +28%\"]",
            "[\"Payment gateway retry delays\"]",
            "[\"Automated account retention\"]",
            "[{\"title\":\"Revenue Anomaly Alerting\",\"category\":\"Revenue\",\"priority\":\"High\",\"rationale\":\"Mitigates risk\"}]");

        mockExecStore
            .Setup(x => x.GetLatestByWorkspaceIdAsync(workspaceId, It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult<ExecutiveReport?>(report));

        var handler = new GetDashboardExecutiveSummaryQueryHandler(mockExecStore.Object);

        // Act
        var result = await handler.HandleAsync(new GetDashboardExecutiveSummaryQuery(workspaceId));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Title.Should().Be("Monthly Executive Intelligence Briefing");
        result.Value.KeyFindings.Should().Contain("Query throughput +28%");
        result.Value.Risks.Should().Contain("Payment gateway retry delays");
        result.Value.Recommendations.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetExecutiveReportByIdQueryHandler_WhenExists_ShouldReturnReport()
    {
        // Arrange
        var workspaceId = Guid.NewGuid();
        var reportId = Guid.NewGuid();
        var mockExecStore = new Mock<IExecutiveReportStore>();

        var report = ExecutiveReport.Create(
            workspaceId,
            "Q3 Board Analytical Briefing",
            "Quarterly",
            "Operating margins stable across all regions.",
            "[\"Revenue +14.2%\"]",
            "[\"Currency FX volatility\"]",
            "[\"Expansion into West region\"]",
            "[{\"title\":\"Optimize collections\",\"category\":\"Finance\",\"priority\":\"High\",\"rationale\":\"Reduces DSO\"}]");

        mockExecStore
            .Setup(x => x.GetByIdAsync(reportId, It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult<ExecutiveReport?>(report));

        var handler = new IntelliQueryAI.Application.Features.ExecutiveIntelligence.Queries.GetExecutiveReportById.GetExecutiveReportByIdQueryHandler(mockExecStore.Object);

        // Act
        var result = await handler.HandleAsync(new IntelliQueryAI.Application.Features.ExecutiveIntelligence.Queries.GetExecutiveReportById.GetExecutiveReportByIdQuery(workspaceId, reportId));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Title.Should().Be("Q3 Board Analytical Briefing");
        result.Value.Findings.Should().Contain("Revenue +14.2%");
        result.Value.Risks.Should().Contain("Currency FX volatility");
    }

    [Fact]
    public async Task GetExecutiveReportByIdQueryHandler_WhenNotFound_ShouldReturnNotFoundError()
    {
        // Arrange
        var workspaceId = Guid.NewGuid();
        var reportId = Guid.NewGuid();
        var mockExecStore = new Mock<IExecutiveReportStore>();

        mockExecStore
            .Setup(x => x.GetByIdAsync(reportId, It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult<ExecutiveReport?>(null));

        var handler = new IntelliQueryAI.Application.Features.ExecutiveIntelligence.Queries.GetExecutiveReportById.GetExecutiveReportByIdQueryHandler(mockExecStore.Object);

        // Act
        var result = await handler.HandleAsync(new IntelliQueryAI.Application.Features.ExecutiveIntelligence.Queries.GetExecutiveReportById.GetExecutiveReportByIdQuery(workspaceId, reportId));

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ExecutiveReport.NotFound");
    }

    [Fact]
    public async Task GetDashboardExecutiveSummaryQueryHandler_WhenReportContainsRawWorkspaceGuid_ShouldSanitizeToFriendlyWorkspaceName()
    {
        // Arrange
        var workspaceId = Guid.NewGuid();
        var mockExecStore = new Mock<IExecutiveReportStore>();
        var mockWorkspaceStore = new Mock<IWorkspaceStore>();

        var slug = IntelliQueryAI.Domain.ValueObjects.TenantSlug.Create("second-workspace").Value;
        var workspace = IntelliQueryAI.Domain.Entities.Workspaces.Workspace.Create("Second WorkSpace", slug, SubscriptionPlan.Free, Guid.NewGuid()).Value;

        mockWorkspaceStore
            .Setup(x => x.GetByIdAsync(workspaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(workspace);

        var report = ExecutiveReport.Create(
            workspaceId,
            "Executive Briefing",
            "October 2026",
            $"Workspace {workspaceId} has recorded two historical queries.",
            $"[\"Activity for workspace {workspaceId} nominal\"]",
            "[]",
            "[]",
            "[]");

        mockExecStore
            .Setup(x => x.GetLatestByWorkspaceIdAsync(workspaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(report);

        var handler = new GetDashboardExecutiveSummaryQueryHandler(mockExecStore.Object, mockWorkspaceStore.Object);

        // Act
        var result = await handler.HandleAsync(new GetDashboardExecutiveSummaryQuery(workspaceId));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Summary.Should().Be("Second WorkSpace has recorded two historical queries.");
        result.Value.Summary.Should().NotContain(workspaceId.ToString());
        result.Value.KeyFindings[0].Should().NotContain(workspaceId.ToString());
    }
}
