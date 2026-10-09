using FluentAssertions;
using IntelliQueryAI.Application.Features.Ai.Queries;
using IntelliQueryAI.Domain.Entities.Ai;
using IntelliQueryAI.Domain.Persistence;
using Moq;
using Xunit;

namespace IntelliQueryAI.Application.Tests;

public sealed class GetPagedQueryHistoryQueryHandlerTests
{
    private readonly Mock<IQueryHistoryStore> _historyStoreMock = new();

    [Fact]
    public async Task HandleAsync_WhenRecordsExist_ReturnsPagedResultWithCorrectMetadata()
    {
        // Arrange
        var workspaceId = Guid.NewGuid();
        var histories = new List<QueryHistory>
        {
            QueryHistory.Create(
                workspaceId,
                Guid.NewGuid(),
                Guid.NewGuid(),
                null,
                "Show revenue",
                "SELECT SUM(amount) FROM sales",
                42,
                10,
                true,
                null,
                0.98),
            QueryHistory.Create(
                workspaceId,
                Guid.NewGuid(),
                Guid.NewGuid(),
                null,
                "Show expenses",
                "SELECT SUM(cost) FROM expenses",
                55,
                5,
                true,
                null,
                0.95)
        };

        _historyStoreMock
            .Setup(s => s.GetPagedByWorkspaceIdAsync(workspaceId, 2, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((histories, 25));

        var handler = new GetPagedQueryHistoryQueryHandler(_historyStoreMock.Object);
        var query = new GetPagedQueryHistoryQuery(workspaceId, Page: 2, PageSize: 10);

        // Act
        var result = await handler.HandleAsync(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Page.Should().Be(2);
        result.Value.PageSize.Should().Be(10);
        result.Value.TotalCount.Should().Be(25);
        result.Value.TotalPages.Should().Be(3);
        result.Value.HasPreviousPage.Should().BeTrue();
        result.Value.HasNextPage.Should().BeTrue();
        result.Value.Items.Should().HaveCount(2);
        result.Value.Items[0].Question.Should().Be("Show revenue");
        result.Value.Items[0].GeneratedSql.Should().Be("SELECT SUM(amount) FROM sales");
    }

    [Fact]
    public async Task HandleAsync_WhenNoRecords_ReturnsEmptyPagedResult()
    {
        // Arrange
        var workspaceId = Guid.NewGuid();
        _historyStoreMock
            .Setup(s => s.GetPagedByWorkspaceIdAsync(workspaceId, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Array.Empty<QueryHistory>(), 0));

        var handler = new GetPagedQueryHistoryQueryHandler(_historyStoreMock.Object);
        var query = new GetPagedQueryHistoryQuery(workspaceId, Page: 1, PageSize: 10);

        // Act
        var result = await handler.HandleAsync(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().BeEmpty();
        result.Value.TotalCount.Should().Be(0);
        result.Value.TotalPages.Should().Be(1);
        result.Value.HasPreviousPage.Should().BeFalse();
        result.Value.HasNextPage.Should().BeFalse();
    }
}
