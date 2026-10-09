using FluentAssertions;
using IntelliQueryAI.Application.Features.Ai.Commands.SaveReport;
using IntelliQueryAI.Application.Features.Ai.Queries;
using IntelliQueryAI.Domain.Entities.Ai;
using IntelliQueryAI.Domain.Persistence;
using Moq;
using Xunit;

namespace IntelliQueryAI.Application.Tests;

public sealed class SaveReportCommandHandlerTests
{
    private readonly Mock<ISavedReportStore> _reportStoreMock = new();

    [Fact]
    public async Task HandleAsync_WhenValidCommand_SavesReportAndReturnsDtoWithConvenienceFields()
    {
        // Arrange
        var workspaceId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _reportStoreMock
            .Setup(s => s.AddAsync(It.IsAny<SavedReport>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = new SaveReportCommandHandler(_reportStoreMock.Object);
        var command = new SaveReportCommand(
            workspaceId,
            userId,
            "Top API Consumers Report",
            "Executive report of API consumer usage",
            "Show me top 20 API consumers",
            "SELECT * FROM api_consumers ORDER BY request_count DESC LIMIT 20",
            "{\"chartType\":\"bar\"}",
            "[\"Top consumer accounts for 45% of traffic\"]");

        // Act
        var result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Title.Should().Be("Top API Consumers Report");
        result.Value.Question.Should().Be("Show me top 20 API consumers");
        result.Value.SqlQuery.Should().Be("SELECT * FROM api_consumers ORDER BY request_count DESC LIMIT 20");
        result.Value.GeneratedSql.Should().Be("SELECT * FROM api_consumers ORDER BY request_count DESC LIMIT 20");
        result.Value.ChartType.Should().Be("bar");
        result.Value.CreatedAt.Should().Be(result.Value.CreatedAtUtc);

        _reportStoreMock.Verify(s => s.AddAsync(It.Is<SavedReport>(r =>
            r.WorkspaceId == workspaceId &&
            r.Title == "Top API Consumers Report" &&
            r.SqlQuery == "SELECT * FROM api_consumers ORDER BY request_count DESC LIMIT 20"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetSavedReportsQueryHandler_WhenReportsExist_ReturnsDtosWithConvenienceFields()
    {
        // Arrange
        var workspaceId = Guid.NewGuid();
        var report = SavedReport.Create(
            workspaceId,
            Guid.NewGuid(),
            "Monthly Revenue Report",
            "Breakdown of revenue by sector",
            "Show monthly revenue",
            "SELECT month, SUM(revenue) FROM sales GROUP BY month",
            "{\"chartType\":\"line\"}",
            "[\"Revenue is up 12%\"]");

        _reportStoreMock
            .Setup(s => s.GetByWorkspaceIdAsync(workspaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SavedReport> { report });

        var handler = new GetSavedReportsQueryHandler(_reportStoreMock.Object);
        var query = new GetSavedReportsQuery(workspaceId);

        // Act
        var result = await handler.HandleAsync(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value[0].Title.Should().Be("Monthly Revenue Report");
        result.Value[0].GeneratedSql.Should().Be("SELECT month, SUM(revenue) FROM sales GROUP BY month");
        result.Value[0].ChartType.Should().Be("line");
        result.Value[0].CreatedAt.Should().Be(result.Value[0].CreatedAtUtc);
    }
}
