using FluentAssertions;
using IntelliQueryAI.Application.Features.BusinessKnowledge.Commands;
using IntelliQueryAI.Application.Features.BusinessKnowledge.Queries;
using IntelliQueryAI.Domain.Entities.Audit;
using IntelliQueryAI.Domain.Entities.BusinessKnowledge;
using IntelliQueryAI.Domain.Persistence;
using Moq;
using Xunit;

namespace IntelliQueryAI.Application.Tests;

public sealed class BusinessKnowledgeTests
{
    private readonly Mock<IBusinessTermStore> _termStoreMock = new();
    private readonly Mock<IMetricStore> _metricStoreMock = new();
    private readonly Mock<IAuditLogStore> _auditStoreMock = new();

    [Fact]
    public async Task CreateBusinessTerm_WhenValid_PersistsAndReturnsDto()
    {
        // Arrange
        var workspaceId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _termStoreMock
            .Setup(s => s.AddAsync(It.IsAny<BusinessTerm>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = new CreateBusinessTermCommandHandler(_termStoreMock.Object, _auditStoreMock.Object);
        var command = new CreateBusinessTermCommand(
            workspaceId,
            userId,
            "Active Customer",
            "Customer with >= 1 successful payment in last 90 days",
            new[] { "Active User", "Retained Customer" },
            "Customer",
            "Internal",
            "Product Ops",
            "ops@company.ng",
            "customers",
            "last_login_at",
            "last_payment_at >= NOW() - INTERVAL '90 days'");

        // Act
        var result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Name.Should().Be("Active Customer");
        result.Value.Category.Should().Be("Customer");
        result.Value.Aliases.Should().Contain("Active User");
        result.Value.SqlCalculationRule.Should().Be("last_payment_at >= NOW() - INTERVAL '90 days'");

        _termStoreMock.Verify(s => s.AddAsync(It.Is<BusinessTerm>(t =>
            t.WorkspaceId == workspaceId && t.Name == "Active Customer"), It.IsAny<CancellationToken>()), Times.Once);

        _auditStoreMock.Verify(s => s.AddAsync(It.Is<AuditLog>(a =>
            a.Action == "BusinessKnowledge.CreateTerm"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateBusinessTerm_WhenWorkspaceMismatched_FailsWithNotFound()
    {
        // Arrange
        var workspaceId = Guid.NewGuid();
        var otherWorkspaceId = Guid.NewGuid();
        var termId = Guid.NewGuid();

        var existingTerm = BusinessTerm.Create(
            otherWorkspaceId,
            "Revenue",
            "Total collections");

        _termStoreMock
            .Setup(s => s.GetByIdAsync(termId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingTerm);

        var handler = new UpdateBusinessTermCommandHandler(_termStoreMock.Object, _auditStoreMock.Object);
        var command = new UpdateBusinessTermCommand(
            workspaceId,
            Guid.NewGuid(),
            termId,
            "Revenue Updated",
            "Updated definition");

        // Act
        var result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("BusinessTerm.NotFound");
    }

    [Fact]
    public async Task CreateMetricDefinition_WhenValid_PersistsAndReturnsDto()
    {
        // Arrange
        var workspaceId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _metricStoreMock
            .Setup(s => s.AddAsync(It.IsAny<MetricDefinition>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = new CreateMetricDefinitionCommandHandler(_metricStoreMock.Object, _auditStoreMock.Object);
        var command = new CreateMetricDefinitionCommand(
            workspaceId,
            userId,
            "Monthly Recurring Revenue",
            "Sum of all active recurring subscription charges normalized to monthly",
            "SUM(sub.monthly_rate)",
            "USD",
            null,
            "sub.status = 'active'",
            "subscriptions",
            "monthly_rate",
            "SUM",
            "HigherIsBetter");

        // Act
        var result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Name.Should().Be("Monthly Recurring Revenue");
        result.Value.CalculationFormula.Should().Be("SUM(sub.monthly_rate)");
        result.Value.Unit.Should().Be("USD");
        result.Value.AggregationType.Should().Be("SUM");

        _metricStoreMock.Verify(s => s.AddAsync(It.Is<MetricDefinition>(m =>
            m.WorkspaceId == workspaceId && m.Name == "Monthly Recurring Revenue"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ResolveSemanticContext_WhenMatchingPrompt_ReturnsCompactRelevantContext()
    {
        // Arrange
        var workspaceId = Guid.NewGuid();
        var prompt = "What is our total revenue from active customers this quarter?";

        var revenueTerm = BusinessTerm.Create(
            workspaceId,
            "Revenue",
            "Total collections from completed transactions",
            new[] { "Income", "Collections" },
            "Finance",
            "Internal",
            targetTable: "payments",
            targetColumn: "amount",
            sqlCalculationRule: "payments.status = 'Successful'");

        var mrrMetric = MetricDefinition.Create(
            workspaceId,
            "Monthly Revenue",
            "Aggregated revenue",
            "SUM(amount)",
            "USD",
            filterExpression: "status = 'Successful'",
            targetTable: "payments");

        _termStoreMock
            .Setup(s => s.SearchRelevantTermsAsync(workspaceId, It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BusinessTerm> { revenueTerm });

        _metricStoreMock
            .Setup(s => s.SearchRelevantMetricsAsync(workspaceId, It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MetricDefinition> { mrrMetric });

        var handler = new ResolveSemanticContextQueryHandler(_termStoreMock.Object, _metricStoreMock.Object);
        var query = new ResolveSemanticContextQuery(workspaceId, prompt);

        // Act
        var result = await handler.HandleAsync(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.RelevantTerms.Should().HaveCount(1);
        result.Value.RelevantTerms[0].Name.Should().Be("Revenue");
        result.Value.RelevantMetrics.Should().HaveCount(1);
        result.Value.RelevantMetrics[0].Name.Should().Be("Monthly Revenue");

        result.Value.FormattedPromptContext.Should().Contain("Term 'Revenue'");
        result.Value.FormattedPromptContext.Should().Contain("payments.status = 'Successful'");
        result.Value.FormattedPromptContext.Should().Contain("Metric 'Monthly Revenue'");
    }
}
