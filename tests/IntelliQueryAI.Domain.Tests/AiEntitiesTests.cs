using FluentAssertions;
using IntelliQueryAI.Domain.Entities.Ai;
using IntelliQueryAI.Domain.Enums;
using Pgvector;
using Xunit;

namespace IntelliQueryAI.Domain.Tests;

public sealed class AiEntitiesTests
{
    [Fact]
    public void EmbeddingRecord_Create_ShouldInitializePropertiesCorrectly()
    {
        // Arrange
        var workspaceId = Guid.NewGuid();
        var dataSourceId = Guid.NewGuid();
        var vector = new Vector(new float[] { 0.1f, 0.2f, 0.3f });

        // Act
        var record = EmbeddingRecord.Create(
            workspaceId,
            dataSourceId,
            EntityType.Table,
            "customers",
            "Table: customers. Description: Profiles",
            vector,
            "{\"schema\":\"public\"}");

        // Assert
        record.Should().NotBeNull();
        record.WorkspaceId.Should().Be(workspaceId);
        record.DataSourceId.Should().Be(dataSourceId);
        record.EntityType.Should().Be(EntityType.Table);
        record.EntityIdentifier.Should().Be("customers");
        record.ContentText.Should().Contain("Table: customers");
        record.Embedding.Should().Be(vector);
        record.MetadataJson.Should().Be("{\"schema\":\"public\"}");
    }

    [Fact]
    public void QueryHistory_Create_ShouldStoreMetricsAndConfidence()
    {
        // Arrange
        var workspaceId = Guid.NewGuid();
        var dataSourceId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        // Act
        var history = QueryHistory.Create(
            workspaceId,
            dataSourceId,
            userId,
            null,
            "Show me top 10 customers",
            "SELECT * FROM customers LIMIT 10",
            42,
            10,
            true,
            null,
            0.95);

        // Assert
        history.Should().NotBeNull();
        history.Question.Should().Be("Show me top 10 customers");
        history.GeneratedSql.Should().Be("SELECT * FROM customers LIMIT 10");
        history.ExecutionTimeMs.Should().Be(42);
        history.RowCount.Should().Be(10);
        history.IsSuccessful.Should().BeTrue();
        history.ConfidenceScore.Should().Be(0.95);
    }

    [Fact]
    public void ConversationSession_CreateAndUpdate_ShouldUpdateLastMessageTimestamp()
    {
        // Arrange
        var session = ConversationSession.Create(Guid.NewGuid(), Guid.NewGuid(), "Q3 Revenue Analysis");
        session.Title.Should().Be("Q3 Revenue Analysis");
        session.ContextSummary.Should().BeNull();

        // Act
        session.UpdateLastMessage("Discussed top accounts and growth projections.");

        // Assert
        session.ContextSummary.Should().Be("Discussed top accounts and growth projections.");
        session.LastMessageAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void SavedReport_CreateAndUpdate_ShouldModifyFieldsAndTimestamp()
    {
        // Arrange
        var report = SavedReport.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Monthly Sales Summary",
            "Executive revenue breakdown",
            "Monthly sales trend",
            "SELECT * FROM sales_monthly",
            "{\"chart\":\"LineChart\"}",
            "[\"Sales grew 12%\"]");

        // Act
        report.Update(
            "Updated Monthly Sales",
            "Refined description",
            "{\"chart\":\"BarChart\"}",
            "[\"Sales grew 15%\"]");

        // Assert
        report.Title.Should().Be("Updated Monthly Sales");
        report.Description.Should().Be("Refined description");
        report.ChartConfigurationJson.Should().Contain("BarChart");
        report.UpdatedAtUtc.Should().NotBeNull();
    }
}
