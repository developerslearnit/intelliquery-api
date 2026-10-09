using IntelliQueryAI.Application.Features.Recommendations.Commands;
using IntelliQueryAI.Application.Features.Recommendations.Dtos;
using IntelliQueryAI.Application.Features.Recommendations.Queries;
using IntelliQueryAI.Domain.Entities.Recommendations;
using IntelliQueryAI.Domain.Persistence;
using Moq;
using Xunit;

namespace IntelliQueryAI.Application.Tests;

public class RecommendationTests
{
    [Fact]
    public void Recommendation_InitialState_ShouldBeActiveWithEvidenceAndSteps()
    {
        // Arrange
        var workspaceId = Guid.NewGuid();
        var evidence = new List<RecommendationEvidenceItem>
        {
            new("High Churn Deviation", "Enterprise churn increased by 14%", "Churn Rate", 0.14m, 0.05m, "Anomaly")
        };
        var actionSteps = new List<RecommendationActionStep>
        {
            new(1, "Engage at-risk accounts", "Retains $150k ARR", false)
        };

        // Act
        var rec = new Recommendation(
            workspaceId: workspaceId,
            createdByUserId: Guid.NewGuid(),
            title: "Remediate Enterprise Churn",
            rationale: "Detected sudden divergence from baseline churn rates.",
            category: "Revenue Retention",
            priority: RecommendationPriority.High,
            targetMetric: "Enterprise Churn Rate",
            impactMagnitude: "+$150k ARR Protected",
            estimatedEffort: "Low (2-3 days)",
            evidence: evidence,
            actionSteps: actionSteps);

        // Assert
        Assert.Equal(RecommendationStatus.Active, rec.Status);
        Assert.Equal(RecommendationPriority.High, rec.Priority);
        Assert.Null(rec.AppliedAtUtc);
        Assert.Null(rec.DismissedAtUtc);
        Assert.Single(rec.GetEvidence());
        Assert.Single(rec.GetActionSteps());
        Assert.False(rec.GetActionSteps()[0].AutomatedActionPossible); // Human-in-the-loop invariant
    }

    [Fact]
    public void Recommendation_MarkApplied_ShouldSetAppliedStateAndTimestamp()
    {
        // Arrange
        var rec = new Recommendation(
            workspaceId: Guid.NewGuid(),
            createdByUserId: Guid.NewGuid(),
            title: "Cost Optimization",
            rationale: "Unused cloud nodes detected.",
            category: "Cost Optimization",
            priority: RecommendationPriority.Medium);

        var operatorUserId = Guid.NewGuid();

        // Act
        rec.MarkApplied(operatorUserId);

        // Assert
        Assert.Equal(RecommendationStatus.Applied, rec.Status);
        Assert.NotNull(rec.AppliedAtUtc);
        Assert.Null(rec.DismissedAtUtc);
    }

    [Fact]
    public void Recommendation_Dismiss_ShouldSetDismissedStateAndReason()
    {
        // Arrange
        var rec = new Recommendation(
            workspaceId: Guid.NewGuid(),
            createdByUserId: Guid.NewGuid(),
            title: "Marketing Channel Shift",
            rationale: "Organic traffic surge.",
            category: "Operational Efficiency",
            priority: RecommendationPriority.Low);

        var operatorUserId = Guid.NewGuid();
        var reason = "Seasonal shift, intentional change in strategy.";

        // Act
        rec.Dismiss(operatorUserId, reason);

        // Assert
        Assert.Equal(RecommendationStatus.Dismissed, rec.Status);
        Assert.NotNull(rec.DismissedAtUtc);
        Assert.Equal(reason, rec.DismissalReason);
    }

    [Fact]
    public void Recommendation_Reactivate_ShouldResetStateToActive()
    {
        // Arrange
        var rec = new Recommendation(
            workspaceId: Guid.NewGuid(),
            createdByUserId: Guid.NewGuid(),
            title: "Governance Fix",
            rationale: "Masking rule missing.",
            category: "Governance & Compliance",
            priority: RecommendationPriority.Critical);

        var operatorUserId = Guid.NewGuid();
        rec.Dismiss(operatorUserId, "Accidental dismissal");

        // Act
        rec.Reactivate(operatorUserId);

        // Assert
        Assert.Equal(RecommendationStatus.Active, rec.Status);
        Assert.Null(rec.DismissedAtUtc);
        Assert.Null(rec.DismissalReason);
    }

    [Fact]
    public async Task CreateRecommendationCommand_ShouldPersistAndReturnDto()
    {
        // Arrange
        var mockStore = new Mock<IRecommendationStore>();
        var handler = new CreateRecommendationCommandHandler(mockStore.Object);

        var workspaceId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var request = new CreateRecommendationRequestDto(
            Title: "Address Payment Gateway Failures",
            Rationale: "Authorization decline rate exceeded 4% alert threshold.",
            Category: "Risk Mitigation",
            Priority: "Critical",
            TargetMetric: "Decline Rate",
            ImpactMagnitude: "+$45,000 Recovered Billings",
            EstimatedEffort: "Immediate",
            Evidence: new List<RecommendationEvidenceItemDto>
            {
                new("Decline Rate Spike", "Spike detected during peak shopping hours", "Decline Rate", 4.2m, 0.8m, "Alert")
            },
            ActionSteps: new List<RecommendationActionStepDto>
            {
                new(1, "Switch secondary failover payment route", "Resolves timeout errors", false)
            });

        var command = new CreateRecommendationCommand(workspaceId, userId, request);

        // Act
        var result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("Address Payment Gateway Failures", result.Value.Title);
        Assert.Equal("Critical", result.Value.Priority);
        Assert.Equal("Active", result.Value.Status);
        Assert.Single(result.Value.Evidence);
        Assert.Single(result.Value.ActionSteps);
        mockStore.Verify(s => s.AddAsync(It.IsAny<Recommendation>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ApplyRecommendationCommand_ShouldEnforceTenantIsolation()
    {
        // Arrange
        var mockStore = new Mock<IRecommendationStore>();
        var handler = new ApplyRecommendationCommandHandler(mockStore.Object);

        var legitimateWorkspaceId = Guid.NewGuid();
        var maliciousWorkspaceId = Guid.NewGuid();
        var recId = Guid.NewGuid();

        var recommendation = new Recommendation(
            workspaceId: legitimateWorkspaceId,
            createdByUserId: Guid.NewGuid(),
            title: "Private Enterprise Action",
            rationale: "Confidential data",
            category: "Strategic",
            priority: RecommendationPriority.High);

        mockStore.Setup(s => s.GetByIdAsync(recId, maliciousWorkspaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Recommendation?)null);

        var command = new ApplyRecommendationCommand(maliciousWorkspaceId, Guid.NewGuid(), recId);

        // Act
        var result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        mockStore.Verify(s => s.UpdateAsync(It.IsAny<Recommendation>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetRecommendationSummaryQuery_ShouldReturnAccurateCounts()
    {
        // Arrange
        var mockStore = new Mock<IRecommendationStore>();
        var workspaceId = Guid.NewGuid();

        mockStore.Setup(s => s.CountByStatusAsync(workspaceId, RecommendationStatus.Active, It.IsAny<CancellationToken>()))
            .ReturnsAsync(7);
        mockStore.Setup(s => s.CountCriticalActiveAsync(workspaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);
        mockStore.Setup(s => s.CountByStatusAsync(workspaceId, RecommendationStatus.Applied, It.IsAny<CancellationToken>()))
            .ReturnsAsync(12);
        mockStore.Setup(s => s.CountByStatusAsync(workspaceId, RecommendationStatus.Dismissed, It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);

        var handler = new GetRecommendationSummaryQueryHandler(mockStore.Object);
        var query = new GetRecommendationSummaryQuery(workspaceId);

        // Act
        var result = await handler.HandleAsync(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(7, result.Value.TotalActive);
        Assert.Equal(2, result.Value.TotalCriticalActive);
        Assert.Equal(12, result.Value.TotalApplied);
        Assert.Equal(3, result.Value.TotalDismissed);
    }
}
