using System.Text.Json;
using FluentAssertions;
using IntelliQueryAI.AI.Agents;
using IntelliQueryAI.AI.Coordinator;
using IntelliQueryAI.Application.Features.Investigations.Commands;
using IntelliQueryAI.Application.Features.Investigations.Dtos;
using IntelliQueryAI.Application.Features.Investigations.Queries;
using IntelliQueryAI.Domain.Entities.Investigations;
using IntelliQueryAI.Domain.Entities.Workspaces;
using IntelliQueryAI.Domain.Persistence;
using IntelliQueryAI.Domain.Services;
using IntelliQueryAI.SharedKernel.Results;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace IntelliQueryAI.Application.Tests;

public sealed class InvestigationTests
{
    private readonly Mock<IInvestigationCoordinator> _coordMock = new();
    private readonly Mock<IInvestigationReportStore> _reportStoreMock = new();
    private readonly Mock<IWorkspaceStore> _workspaceStoreMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<ITenantContext> _tenantContextMock = new();
    private readonly Mock<ILogger<StartInvestigationCommandHandler>> _loggerMock = new();

    [Fact]
    public async Task StartInvestigation_WhenValid_CoordinatesAndPersistsReport()
    {
        // Arrange
        var workspaceId = Guid.NewGuid();
        var slug = IntelliQueryAI.Domain.ValueObjects.TenantSlug.Create("acme-fin").Value;
        var workspace = IntelliQueryAI.Domain.Entities.Workspaces.Workspace.Create("Acme Financial", slug, IntelliQueryAI.Domain.Enums.SubscriptionPlan.Free, Guid.NewGuid()).Value;
        _workspaceStoreMock.Setup(s => s.GetByIdAsync(workspaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(workspace);

        var coordResponse = new InvestigationCoordinatorResponse(
            Summary: "Revenue contracted by 12% due to enterprise churn.",
            ConfidenceScore: 0.93,
            Status: "Completed",
            RootCauses: new[] { "3 enterprise renewals lapsed in EU region." },
            Contributors: new[]
            {
                new InvestigationContributor(
                    Dimension: "CustomerTier",
                    Segment: "Enterprise",
                    ContributionPercentage: -65.0,
                    ImpactDirection: "Negative",
                    BaselineValue: 300000m,
                    CurrentValue: 105000m,
                    ChangeValue: -195000m,
                    Explanation: "Loss of 3 key contracts."
                )
            },
            Evidence: new[]
            {
                new InvestigationEvidence(
                    Title: "Quarterly Revenue Deviation",
                    Description: "Sharp divergence starting Week 3.",
                    EvidenceType: "MetricChange",
                    MetricName: "Revenue",
                    DataPointsJson: "[{\"period\":\"Q1\",\"value\":300000}]",
                    ChartType: "Line"
                )
            },
            Recommendations: new[]
            {
                new ActionableRecommendation(
                    Title: "Targeted Account Outreach",
                    Category: "CustomerSuccess",
                    Priority: "Critical",
                    Rationale: "Concentrated churn.",
                    ActionSteps: new[] { "Reach out to accounts immediately" }
                )
            },
            QueriesExecuted: new[]
            {
                new InvestigationQueryExecuted(
                    Purpose: "Dimension breakdown",
                    SqlQuery: "SELECT tier, sum(amount) FROM payments GROUP BY tier",
                    RowCount: 3,
                    DurationMs: 25
                )
            },
            FollowUpQuestions: new[] { "Which specific accounts churned?" },
            DurationMs: 1450,
            TokensUsed: 620
        );

        _coordMock.Setup(c => c.InvestigateAsync(It.IsAny<InvestigationCoordinatorRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(coordResponse));

        _reportStoreMock.Setup(s => s.AddAsync(It.IsAny<InvestigationReport>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var handler = new StartInvestigationCommandHandler(
            _coordMock.Object,
            _reportStoreMock.Object,
            _workspaceStoreMock.Object,
            _unitOfWorkMock.Object,
            _tenantContextMock.Object,
            _loggerMock.Object);

        var command = new StartInvestigationCommand(
            Question: "Why did revenue drop last month?",
            WorkspaceId: workspaceId);

        // Act
        var result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Summary.Should().Contain("Revenue contracted");
        result.Value.ConfidenceScore.Should().Be(0.93);
        result.Value.Contributors.Should().HaveCount(1);
        result.Value.Contributors[0].Dimension.Should().Be("CustomerTier");
        result.Value.Evidence.Should().HaveCount(1);
        result.Value.Recommendations.Should().HaveCount(1);
        result.Value.FollowUpQuestions.Should().Contain("Which specific accounts churned?");

        _reportStoreMock.Verify(s => s.AddAsync(It.Is<InvestigationReport>(r =>
            r.WorkspaceId == workspaceId && r.ConfidenceScore == 0.93), It.IsAny<CancellationToken>()), Times.Once);

        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task StartInvestigation_WhenTenantMissing_FailsWithUnauthorized()
    {
        // Arrange
        _tenantContextMock.Setup(t => t.WorkspaceId).Returns((Guid?)null);

        var handler = new StartInvestigationCommandHandler(
            _coordMock.Object,
            _reportStoreMock.Object,
            _workspaceStoreMock.Object,
            _unitOfWorkMock.Object,
            _tenantContextMock.Object,
            _loggerMock.Object);

        var command = new StartInvestigationCommand(
            Question: "Why did orders decline?",
            WorkspaceId: null);

        // Act
        var result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Tenant.Missing");
    }

    [Fact]
    public async Task GetInvestigationById_WhenWorkspaceMismatched_FailsWithNotFound()
    {
        // Arrange
        var workspaceId = Guid.NewGuid();
        var otherWorkspaceId = Guid.NewGuid();
        var reportId = Guid.NewGuid();

        var existingReport = InvestigationReport.Create(
            otherWorkspaceId,
            null,
            "Why did sales drop?",
            null,
            "Summary text",
            0.9,
            "Completed",
            "[]", "[]", "[]", "[]", "[]", "[]",
            500, 100);

        _reportStoreMock.Setup(s => s.GetByIdAsync(reportId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingReport);

        var handler = new GetInvestigationByIdQueryHandler(
            _reportStoreMock.Object,
            _tenantContextMock.Object);

        var query = new GetInvestigationByIdQuery(reportId, workspaceId);

        // Act
        var result = await handler.HandleAsync(query, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Investigation.NotFound");
    }

    [Fact]
    public async Task GetInvestigations_ReturnsMappedDtos()
    {
        // Arrange
        var workspaceId = Guid.NewGuid();
        var reports = new List<InvestigationReport>
        {
            InvestigationReport.Create(
                workspaceId, null, "Question 1", null, "Summary 1", 0.9, "Completed",
                "[]", "[]", "[]", "[]", "[]", "[]", 100, 50),
            InvestigationReport.Create(
                workspaceId, null, "Question 2", null, "Summary 2", 0.85, "Completed",
                "[]", "[]", "[]", "[]", "[]", "[]", 150, 60)
        };

        _reportStoreMock.Setup(s => s.GetByWorkspaceIdAsync(workspaceId, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reports);

        var handler = new GetInvestigationsQueryHandler(
            _reportStoreMock.Object,
            _tenantContextMock.Object);

        var query = new GetInvestigationsQuery(workspaceId);

        // Act
        var result = await handler.HandleAsync(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value[0].Question.Should().Be("Question 1");
        result.Value[1].Question.Should().Be("Question 2");
    }
}
