using FluentAssertions;
using IntelliQueryAI.AI.Embeddings;
using IntelliQueryAI.AI.Evaluations;
using IntelliQueryAI.AI.Telemetry;
using IntelliQueryAI.AI.Tools;
using IntelliQueryAI.Application.Features.DataSources.Commands.RegisterDataSource;
using IntelliQueryAI.Domain.Entities.Audit;
using IntelliQueryAI.Domain.Entities.DataSources;
using IntelliQueryAI.Domain.Entities.Subscriptions;
using IntelliQueryAI.Domain.Enums;
using IntelliQueryAI.Domain.Persistence;
using IntelliQueryAI.Domain.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Pgvector;
using Xunit;

namespace IntelliQueryAI.Application.Tests;

public sealed class SchemaRetrievalAndEbsQueryTests
{
    private readonly Mock<IVectorStore> _vectorStoreMock = new();
    private readonly Mock<ISchemaReadStore> _schemaReadStoreMock = new();
    private readonly Mock<ITelemetryService> _telemetryMock = new();
    private readonly Mock<IEmbeddingGenerator> _embeddingGeneratorMock = new();
    private readonly SchemaRetrievalTools _schemaTools;
    private readonly AgentEvaluationService _evaluationService = new();

    public SchemaRetrievalAndEbsQueryTests()
    {
        _schemaTools = new SchemaRetrievalTools(
            _vectorStoreMock.Object,
            _schemaReadStoreMock.Object,
            _telemetryMock.Object);
    }

    [Fact]
    public async Task RetrieveRelevantSchemaAsync_WhenUserAsksForEbsTransactions_ShouldIncludeAndRankEbsTransactionsTableAtTop()
    {
        // Arrange
        var workspaceId = Guid.NewGuid();
        var dataSourceId = Guid.NewGuid();

        // Simulate 25 database tables (alphabetically ordered, ebs_transactions at index 12)
        var tables = new List<TableSummaryDto>();
        for (int i = 0; i < 25; i++)
        {
            string tName = i == 12 ? "ebs_transactions" : $"table_{i:D2}";
            string fName = i == 12 ? "Ebs Transactions" : $"Table {i:D2}";
            tables.Add(new TableSummaryDto(dataSourceId, "SqlServer DB", "dbo", tName, fName, 5));
        }

        _schemaReadStoreMock.Setup(s => s.GetTablesAsync(workspaceId, dataSourceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tables);

        var columns = new List<ColumnMetadataDto>
        {
            new(Guid.NewGuid(), dataSourceId, "dbo", "ebs_transactions", "transaction_id", "bigint", false, true, false, null, "Ebs Transactions", "Transaction ID", 1),
            new(Guid.NewGuid(), dataSourceId, "dbo", "ebs_transactions", "amount", "decimal(18,2)", false, false, false, null, "Ebs Transactions", "Amount", 2),
            new(Guid.NewGuid(), dataSourceId, "dbo", "ebs_transactions", "status", "varchar(50)", false, false, false, null, "Ebs Transactions", "Status", 3),
            new(Guid.NewGuid(), dataSourceId, "dbo", "ebs_transactions", "created_at", "datetime2", false, false, false, null, "Ebs Transactions", "Created At", 4)
        };

        _schemaReadStoreMock.Setup(s => s.GetColumnsAsync(workspaceId, dataSourceId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(columns);

        _schemaReadStoreMock.Setup(s => s.GetRelationshipsAsync(workspaceId, dataSourceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RelationshipDto>());

        var dummyVector = new Vector(new float[1536]);

        // Act
        var result = await _schemaTools.RetrieveRelevantSchemaAsync(
            workspaceId,
            dataSourceId,
            userQuestion: "Show me top 20 ebs transactions",
            queryEmbedding: dummyVector,
            topK: 10);

        // Assert
        result.RelevantTables.Should().Contain("ebs_transactions");
        result.RelevantTables.First().Should().Be("ebs_transactions", "because it has the highest lexical match to 'ebs' and 'transactions'");
        result.FormattedText.Should().Contain("Table: dbo.ebs_transactions (Business: Ebs Transactions)");
        result.FormattedText.Should().Contain("transaction_id (bigint [PK]");
        result.FormattedText.Should().Contain("amount (decimal(18,2)");
    }

    [Fact]
    public void EvaluateSql_WhenLlmOutputsDummySchemaNotFoundMessage_ShouldRejectWithInvalidResult()
    {
        // Arrange: Exact user symptom SQL output
        string dummyMessageSql = """
            SELECT TOP (20)
                N'The provided schema does not include an EBS transactions table. Please provide its table name and columns.' AS [Message];
            """;

        // Act
        var eval = _evaluationService.EvaluateSql(dummyMessageSql, Array.Empty<string>(), Array.Empty<string>());

        // Assert
        eval.IsValid.Should().BeFalse("placeholder error message queries must not be allowed as valid data queries");
        eval.ConfidenceScore.Should().Be(0.0);
        eval.Issues.Should().Contain(i => i.Contains("explanatory message"));
    }

    [Fact]
    public async Task RegisterDataSourceCommandHandler_ShouldAutomaticallyDiscoverAndIndexSchemaUponCreation()
    {
        // Arrange
        var dsStoreMock = new Mock<IDataSourceStore>();
        var subStoreMock = new Mock<ISubscriptionStore>();
        var encServiceMock = new Mock<IEncryptionService>();
        var discoveryMock = new Mock<ISchemaDiscoveryEngine>();
        var auditMock = new Mock<IAuditLogStore>();
        var uowMock = new Mock<IUnitOfWork>();
        var schemaMetadataMock = new Mock<ISchemaMetadataStore>();
        var embeddingIndexerMock = new Mock<ISchemaEmbeddingIndexer>();

        var workspaceId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var command = new RegisterDataSourceCommand(
            workspaceId,
            "Financial EBS DB",
            DatabaseType.SqlServer,
            "Server=ebs-host;Database=ebs_prod;",
            userId);

        subStoreMock.Setup(s => s.GetByWorkspaceIdAsync(workspaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Subscription.CreateDefault(workspaceId, SubscriptionPlan.Enterprise));

        dsStoreMock.Setup(d => d.CountByWorkspaceIdAsync(workspaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        discoveryMock.Setup(d => d.TestConnectionAsync(command.DatabaseType, command.ConnectionString, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        encServiceMock.Setup(e => e.Encrypt(command.ConnectionString))
            .Returns("encrypted_ebs_secret");

        var discoveredCols = new List<SchemaMetadata>
        {
            SchemaMetadata.Create(workspaceId, Guid.NewGuid(), "dbo", "ebs_transactions", "txn_id", "bigint", false, true, false, null, "Ebs Transactions", "Txn ID", 1)
        };

        discoveryMock.Setup(d => d.DiscoverSchemaAsync(workspaceId, It.IsAny<Guid>(), command.DatabaseType, command.ConnectionString, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DiscoveredSchemaResult(discoveredCols, new List<TableRelationship>(), new List<DatabaseRoutine>()));

        var handler = new RegisterDataSourceCommandHandler(
            dsStoreMock.Object,
            subStoreMock.Object,
            encServiceMock.Object,
            discoveryMock.Object,
            auditMock.Object,
            uowMock.Object,
            schemaMetadataMock.Object,
            embeddingIndexerMock.Object);

        // Act
        var result = await handler.HandleAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.SyncStatus.Should().Be("Completed", "initial automated discovery should mark sync completed upon registration");

        schemaMetadataMock.Verify(s => s.ReplaceSchemaAsync(
            workspaceId,
            It.IsAny<Guid>(),
            It.Is<IReadOnlyList<SchemaMetadata>>(c => c.Count == 1),
            It.IsAny<IReadOnlyList<TableRelationship>>(),
            It.IsAny<IReadOnlyList<DatabaseRoutine>>(),
            It.IsAny<CancellationToken>()), Times.Once);

        embeddingIndexerMock.Verify(e => e.IndexSchemaAsync(
            workspaceId,
            It.IsAny<Guid>(),
            It.Is<IReadOnlyList<SchemaMetadata>>(c => c.Count == 1),
            It.IsAny<IReadOnlyList<TableRelationship>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
