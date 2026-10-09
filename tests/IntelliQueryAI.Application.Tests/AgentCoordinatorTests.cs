using FluentAssertions;
using IntelliQueryAI.AI.Agents;
using IntelliQueryAI.AI.Common;
using IntelliQueryAI.AI.Coordinator;
using IntelliQueryAI.AI.Embeddings;
using IntelliQueryAI.AI.Evaluations;
using IntelliQueryAI.AI.Memory;
using IntelliQueryAI.AI.Telemetry;
using IntelliQueryAI.AI.Tools;
using IntelliQueryAI.Domain.Entities.Ai;
using IntelliQueryAI.Domain.Entities.DataSources;
using IntelliQueryAI.Domain.Enums;
using IntelliQueryAI.Domain.Errors;
using IntelliQueryAI.Domain.Persistence;
using IntelliQueryAI.Domain.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Pgvector;
using Xunit;

namespace IntelliQueryAI.Application.Tests;

public sealed class AgentCoordinatorTests
{
    private readonly Mock<IDataSourceStore> _dataSourceStoreMock = new();
    private readonly Mock<IEncryptionService> _encryptionServiceMock = new();
    private readonly Mock<IQueryHistoryStore> _queryHistoryStoreMock = new();
    private readonly Mock<IVectorStore> _vectorStoreMock = new();
    private readonly Mock<ISchemaReadStore> _schemaReadStoreMock = new();
    private readonly Mock<ISafeQueryExecutor> _safeExecutorMock = new();
    private readonly AgentCoordinator _coordinator;

    public AgentCoordinatorTests()
    {
        var telemetryMock = new Mock<ITelemetryService>();
        var evalService = new AgentEvaluationService();
        var memoryService = new AgentMemoryService();
        var optionsMock = Options.Create(new AzureOpenAISettings());
        var simulationMode = new SimulationMode(true);

        var intentAgent = new IntentUnderstandingAgent(null, telemetryMock.Object, new Mock<ILogger<IntentUnderstandingAgent>>().Object, optionsMock, simulationMode);
        var sqlGenAgent = new SqlGenerationAgent(null, telemetryMock.Object, new Mock<ILogger<SqlGenerationAgent>>().Object, optionsMock, simulationMode);
        var sqlValidationAgent = new SqlValidationAgent(null, evalService, telemetryMock.Object, new Mock<ILogger<SqlValidationAgent>>().Object, optionsMock, simulationMode);
        var dataAnalystAgent = new DataAnalystAgent(null, telemetryMock.Object, new Mock<ILogger<DataAnalystAgent>>().Object, optionsMock, simulationMode);
        var visualizationAgent = new VisualizationAgent(null, telemetryMock.Object, new Mock<ILogger<VisualizationAgent>>().Object, optionsMock, simulationMode);

        var schemaTools = new SchemaRetrievalTools(_vectorStoreMock.Object, _schemaReadStoreMock.Object, telemetryMock.Object);
        var sqlExecutionTools = new SqlExecutionTools(_safeExecutorMock.Object, telemetryMock.Object);
        var embeddingGenerator = new AzureOpenAIEmbeddingGenerator(null, simulationMode, new Mock<ILogger<AzureOpenAIEmbeddingGenerator>>().Object);

        _coordinator = new AgentCoordinator(
            intentAgent,
            sqlGenAgent,
            sqlValidationAgent,
            dataAnalystAgent,
            visualizationAgent,
            schemaTools,
            sqlExecutionTools,
            embeddingGenerator,
            _dataSourceStoreMock.Object,
            _encryptionServiceMock.Object,
            _queryHistoryStoreMock.Object,
            memoryService,
            telemetryMock.Object,
            new Mock<ILogger<AgentCoordinator>>().Object);
    }

    [Fact]
    public async Task ExecuteQueryPipelineAsync_WhenValid_ShouldRunAllAgentsAndReturnCompleteResponse()
    {
        // Arrange
        var workspaceId = Guid.NewGuid();
        var dataSourceId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var dataSource = DataSource.Create(workspaceId, "Production DB", DatabaseType.PostgreSQL, "enc_connection").Value;
        _dataSourceStoreMock.Setup(d => d.GetByIdAsync(dataSourceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dataSource);

        _encryptionServiceMock.Setup(e => e.Decrypt("enc_connection"))
            .Returns("Host=localhost;Database=test;");

        _vectorStoreMock.Setup(v => v.SearchSimilarAsync(workspaceId, dataSourceId, It.IsAny<Vector>(), It.IsAny<int>(), It.IsAny<double>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VectorSearchResult>
            {
                new(Guid.NewGuid(), workspaceId, dataSourceId, "Table", "customers", "Table: customers", null, 0.92)
            });

        _schemaReadStoreMock.Setup(s => s.GetColumnsAsync(workspaceId, dataSourceId, "customers", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ColumnMetadataDto>
            {
                new(Guid.NewGuid(), dataSourceId, "public", "customers", "id", "integer", false, true, false, null, "Customer", "ID", 1),
                new(Guid.NewGuid(), dataSourceId, "public", "customers", "name", "varchar", false, false, false, null, "Customer", "Name", 2)
            });

        _schemaReadStoreMock.Setup(s => s.GetRelationshipsAsync(workspaceId, dataSourceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RelationshipDto>());

        var execResult = new ExecutionQueryResult(
            Columns: ["customer_id", "customer_name", "total_orders", "total_revenue"],
            Rows: new List<Dictionary<string, object?>>
            {
                new() { { "customer_id", 1 }, { "customer_name", "Acme Corp" }, { "total_orders", 42 }, { "total_revenue", 12500.0 } }
            },
            RowCount: 1,
            ExecutionDurationMs: 45,
            IsSuccess: true,
            ErrorMessage: null);

        _safeExecutorMock.Setup(e => e.ExecuteSafeQueryAsync(DatabaseType.PostgreSQL, "Host=localhost;Database=test;", It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(execResult);

        var request = new CoordinatorQueryRequest(
            workspaceId,
            dataSourceId,
            userId,
            "Show me top customers by revenue");

        // Act
        var result = await _coordinator.ExecuteQueryPipelineAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Question.Should().Be("Show me top customers by revenue");
        result.Value.GeneratedSql.Should().Contain("SELECT");
        result.Value.Results.RowCount.Should().Be(1);
        result.Value.Insights.Should().NotBeEmpty();
        result.Value.RecommendedChart.Should().NotBeNull();
        result.Value.ConfidenceScore.Should().BeGreaterThan(0.5);

        _queryHistoryStoreMock.Verify(q => q.AddAsync(It.IsAny<QueryHistory>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteQueryPipelineAsync_WhenDataSourceNotFound_ShouldReturnFailure()
    {
        // Arrange
        var request = new CoordinatorQueryRequest(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "What is our revenue?");
        _dataSourceStoreMock.Setup(d => d.GetByIdAsync(request.DataSourceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DataSource?)null);

        // Act
        var result = await _coordinator.ExecuteQueryPipelineAsync(request);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(DataSourceErrors.NotFound);
    }

    [Fact]
    public async Task ExecuteQueryPipelineAsync_WhenDataSourceInactive_ShouldReturnInactiveError()
    {
        // Arrange
        var workspaceId = Guid.NewGuid();
        var dataSource = DataSource.Create(workspaceId, "Disabled DB", DatabaseType.PostgreSQL, "enc").Value;
        dataSource.Disable();

        _dataSourceStoreMock.Setup(d => d.GetByIdAsync(dataSource.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dataSource);

        var request = new CoordinatorQueryRequest(workspaceId, dataSource.Id, Guid.NewGuid(), "Count users");

        // Act
        var result = await _coordinator.ExecuteQueryPipelineAsync(request);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(DataSourceErrors.Inactive);
    }
}
