using FluentAssertions;
using IntelliQueryAI.Application.Features.DataSources.Commands.SyncDataSource;
using IntelliQueryAI.Domain.Entities.Audit;
using IntelliQueryAI.Domain.Entities.DataSources;
using IntelliQueryAI.Domain.Enums;
using IntelliQueryAI.Domain.Errors;
using IntelliQueryAI.Domain.Persistence;
using IntelliQueryAI.Domain.Services;
using Moq;
using Xunit;

namespace IntelliQueryAI.Application.Tests;

public sealed class SyncDataSourceCommandHandlerTests
{
    private readonly Mock<IDataSourceStore> _dataSourceStoreMock = new();
    private readonly Mock<ISchemaMetadataStore> _schemaMetadataStoreMock = new();
    private readonly Mock<IEncryptionService> _encryptionServiceMock = new();
    private readonly Mock<ISchemaDiscoveryEngine> _discoveryEngineMock = new();
    private readonly Mock<IAuditLogStore> _auditLogStoreMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly SyncDataSourceCommandHandler _handler;

    public SyncDataSourceCommandHandlerTests()
    {
        _handler = new SyncDataSourceCommandHandler(
            _dataSourceStoreMock.Object,
            _schemaMetadataStoreMock.Object,
            _encryptionServiceMock.Object,
            _discoveryEngineMock.Object,
            _auditLogStoreMock.Object,
            _unitOfWorkMock.Object);
    }

    [Fact]
    public async Task HandleAsync_WhenDataSourceNotFound_ShouldReturnNotFoundError()
    {
        // Arrange
        var command = new SyncDataSourceCommand(Guid.NewGuid(), Guid.NewGuid());
        _dataSourceStoreMock.Setup(d => d.GetByIdAsync(command.DataSourceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DataSource?)null);

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(DataSourceErrors.NotFound);
    }

    [Fact]
    public async Task HandleAsync_WhenDataSourceInactive_ShouldReturnInactiveError()
    {
        // Arrange
        var workspaceId = Guid.NewGuid();
        var dataSource = DataSource.Create(workspaceId, "Warehouse DB", DatabaseType.PostgreSQL, "enc_str").Value;
        dataSource.Disable();

        var command = new SyncDataSourceCommand(dataSource.Id, workspaceId);
        _dataSourceStoreMock.Setup(d => d.GetByIdAsync(dataSource.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dataSource);

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(DataSourceErrors.Inactive);
    }

    [Fact]
    public async Task HandleAsync_WhenValid_ShouldExtractSchemaAndCompleteSync()
    {
        // Arrange
        var workspaceId = Guid.NewGuid();
        var dataSource = DataSource.Create(workspaceId, "CRM DB", DatabaseType.PostgreSQL, "enc_str").Value;
        var command = new SyncDataSourceCommand(dataSource.Id, workspaceId, Guid.NewGuid());

        _dataSourceStoreMock.Setup(d => d.GetByIdAsync(dataSource.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dataSource);

        _encryptionServiceMock.Setup(e => e.Decrypt("enc_str")).Returns("raw_connection_string");

        var columns = new List<SchemaMetadata>
        {
            SchemaMetadata.Create(workspaceId, dataSource.Id, "public", "tbl_customer", "customer_id", "integer", false, true, false, null, "Customer", "Customer ID", 1),
            SchemaMetadata.Create(workspaceId, dataSource.Id, "public", "tbl_customer", "email", "varchar(255)", false, false, false, null, "Customer", "Email", 2),
            SchemaMetadata.Create(workspaceId, dataSource.Id, "public", "tbl_order", "order_id", "integer", false, true, false, null, "Order", "Order ID", 1),
            SchemaMetadata.Create(workspaceId, dataSource.Id, "public", "tbl_order", "customer_id", "integer", false, false, true, null, "Order", "Customer ID", 2)
        };

        var relationships = new List<TableRelationship>
        {
            TableRelationship.Create(workspaceId, dataSource.Id, "fk_order_customer", "tbl_customer", "customer_id", "tbl_order", "customer_id")
        };

        var routines = new List<DatabaseRoutine>
        {
            DatabaseRoutine.Create(workspaceId, dataSource.Id, "usp_get_customer_orders", "PROCEDURE", "SELECT * FROM tbl_order WHERE customer_id = p_id", "Get Customer Orders")
        };

        var discoveryResult = new DiscoveredSchemaResult(columns, relationships, routines);
        _discoveryEngineMock.Setup(e => e.DiscoverSchemaAsync(workspaceId, dataSource.Id, DatabaseType.PostgreSQL, "raw_connection_string", It.IsAny<CancellationToken>()))
            .ReturnsAsync(discoveryResult);

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.TablesCount.Should().Be(2);
        result.Value.ColumnsCount.Should().Be(4);
        result.Value.RelationshipsCount.Should().Be(1);
        result.Value.RoutinesCount.Should().Be(1);
        result.Value.Success.Should().BeTrue();

        dataSource.SyncStatus.Should().Be(SyncStatus.Completed);
        dataSource.LastSyncAt.Should().NotBeNull();

        _schemaMetadataStoreMock.Verify(s => s.ReplaceSchemaAsync(
            workspaceId,
            dataSource.Id,
            It.Is<IReadOnlyList<SchemaMetadata>>(c => c.Count == 4),
            It.Is<IReadOnlyList<TableRelationship>>(r => r.Count == 1),
            It.Is<IReadOnlyList<DatabaseRoutine>>(rt => rt.Count == 1),
            It.IsAny<CancellationToken>()), Times.Once);

        _auditLogStoreMock.Verify(a => a.AddAsync(It.Is<AuditLog>(l => l.Action == "DataSource.SyncCompleted"), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task HandleAsync_WhenDiscoveryFails_ShouldMarkSyncFailedAndReturnError()
    {
        // Arrange
        var workspaceId = Guid.NewGuid();
        var dataSource = DataSource.Create(workspaceId, "CRM DB", DatabaseType.PostgreSQL, "enc_str").Value;
        var command = new SyncDataSourceCommand(dataSource.Id, workspaceId);

        _dataSourceStoreMock.Setup(d => d.GetByIdAsync(dataSource.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dataSource);

        _encryptionServiceMock.Setup(e => e.Decrypt("enc_str")).Returns("raw_connection_string");

        _discoveryEngineMock.Setup(e => e.DiscoverSchemaAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<DatabaseType>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SSL handshake failed."));

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(DataSourceErrors.ConnectionFailed("").Code);

        dataSource.SyncStatus.Should().Be(SyncStatus.Failed);
        dataSource.SyncErrorMessage.Should().Contain("SSL handshake failed.");

        _schemaMetadataStoreMock.Verify(s => s.ReplaceSchemaAsync(
            It.IsAny<Guid>(),
            It.IsAny<Guid>(),
            It.IsAny<IReadOnlyList<SchemaMetadata>>(),
            It.IsAny<IReadOnlyList<TableRelationship>>(),
            It.IsAny<IReadOnlyList<DatabaseRoutine>>(),
            It.IsAny<CancellationToken>()), Times.Never);

        _auditLogStoreMock.Verify(a => a.AddAsync(It.Is<AuditLog>(l => l.Action == "DataSource.SyncFailed"), It.IsAny<CancellationToken>()), Times.Once);
    }
}
