using System.Diagnostics;
using FluentValidation;
using IntelliQueryAI.Application.Abstractions.Messaging;
using IntelliQueryAI.Application.Features.DataSources.Dtos;
using IntelliQueryAI.Domain.Entities.Audit;
using IntelliQueryAI.Domain.Errors;
using IntelliQueryAI.Domain.Persistence;
using IntelliQueryAI.Domain.Services;
using IntelliQueryAI.SharedKernel.Results;

namespace IntelliQueryAI.Application.Features.DataSources.Commands.SyncDataSource;

/// <summary>
/// Command to trigger a synchronous or immediate schema discovery and metadata refresh for a data source.
/// </summary>
public sealed record SyncDataSourceCommand(
    Guid DataSourceId,
    Guid WorkspaceId,
    Guid? TriggeredByUserId = null) : ICommand<Result<SyncResultDto>>;

public sealed class SyncDataSourceCommandValidator : AbstractValidator<SyncDataSourceCommand>
{
    public SyncDataSourceCommandValidator()
    {
        RuleFor(x => x.DataSourceId).NotEmpty().WithMessage("DataSourceId is required.");
        RuleFor(x => x.WorkspaceId).NotEmpty().WithMessage("WorkspaceId is required.");
    }
}

public sealed class SyncDataSourceCommandHandler : ICommandHandler<SyncDataSourceCommand, Result<SyncResultDto>>
{
    private readonly IDataSourceStore _dataSourceStore;
    private readonly ISchemaMetadataStore _schemaMetadataStore;
    private readonly IEncryptionService _encryptionService;
    private readonly ISchemaDiscoveryEngine _discoveryEngine;
    private readonly IAuditLogStore _auditLogStore;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IntelliQueryAI.AI.Embeddings.ISchemaEmbeddingIndexer? _embeddingIndexer;

    public SyncDataSourceCommandHandler(
        IDataSourceStore dataSourceStore,
        ISchemaMetadataStore schemaMetadataStore,
        IEncryptionService encryptionService,
        ISchemaDiscoveryEngine discoveryEngine,
        IAuditLogStore auditLogStore,
        IUnitOfWork unitOfWork,
        IntelliQueryAI.AI.Embeddings.ISchemaEmbeddingIndexer? embeddingIndexer = null)
    {
        _dataSourceStore = dataSourceStore;
        _schemaMetadataStore = schemaMetadataStore;
        _encryptionService = encryptionService;
        _discoveryEngine = discoveryEngine;
        _auditLogStore = auditLogStore;
        _unitOfWork = unitOfWork;
        _embeddingIndexer = embeddingIndexer;
    }

    public async Task<Result<SyncResultDto>> HandleAsync(SyncDataSourceCommand command, CancellationToken cancellationToken = default)
    {
        var dataSource = await _dataSourceStore.GetByIdAsync(command.DataSourceId, cancellationToken);
        if (dataSource is null || dataSource.WorkspaceId != command.WorkspaceId)
        {
            return Result.Failure<SyncResultDto>(DataSourceErrors.NotFound);
        }

        if (!dataSource.IsActive)
        {
            return Result.Failure<SyncResultDto>(DataSourceErrors.Inactive);
        }

        if (dataSource.SyncStatus == Domain.Enums.SyncStatus.InProgress)
        {
            return Result.Failure<SyncResultDto>(DataSourceErrors.SyncInProgress);
        }

        dataSource.MarkSyncStarted();
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var sw = Stopwatch.StartNew();
        try
        {
            string connectionString = _encryptionService.Decrypt(dataSource.EncryptedConnectionString);

            // Execute automated schema discovery across tables, columns, PKs, FKs, views, and stored procedures
            var discovery = await _discoveryEngine.DiscoverSchemaAsync(
                command.WorkspaceId,
                command.DataSourceId,
                dataSource.DatabaseType,
                connectionString,
                cancellationToken);

            // Atomically replace schema snapshot in metadata store
            await _schemaMetadataStore.ReplaceSchemaAsync(
                command.WorkspaceId,
                command.DataSourceId,
                discovery.Columns,
                discovery.Relationships,
                discovery.Routines,
                cancellationToken);

            // Index semantic vector embeddings into PgVector
            if (_embeddingIndexer != null)
            {
                await _embeddingIndexer.IndexSchemaAsync(
                    command.WorkspaceId,
                    command.DataSourceId,
                    discovery.Columns,
                    discovery.Relationships,
                    cancellationToken);
            }

            dataSource.MarkSyncCompleted();
            sw.Stop();

            await _auditLogStore.AddAsync(AuditLog.Create(
                command.WorkspaceId,
                command.TriggeredByUserId,
                "DataSource.SyncCompleted",
                "DataSource",
                dataSource.Id.ToString(),
                $"Discovered {discovery.Columns.Select(c => c.TableName).Distinct().Count()} tables, {discovery.Columns.Count} columns, and {discovery.Relationships.Count} relationships in {sw.ElapsedMilliseconds}ms."),
                cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            int tablesCount = discovery.Columns.Select(c => c.TableName).Distinct(StringComparer.OrdinalIgnoreCase).Count();
            int columnsCount = discovery.Columns.Count;
            int relationshipsCount = discovery.Relationships.Count;
            int routinesCount = discovery.Routines.Count;

            string syncMessage = tablesCount > 0
                ? $"Successfully discovered {tablesCount} tables, {columnsCount} columns, and {relationshipsCount} relationships in {sw.ElapsedMilliseconds}ms."
                : $"Sync finished in {sw.ElapsedMilliseconds}ms, but 0 user tables were discovered. Please verify user tables exist and database user permissions.";

            return new SyncResultDto(
                dataSource.Id,
                tablesCount,
                columnsCount,
                relationshipsCount,
                routinesCount,
                sw.ElapsedMilliseconds,
                true,
                syncMessage);
        }
        catch (Exception ex)
        {
            sw.Stop();
            dataSource.MarkSyncFailed(ex.Message);
            await _auditLogStore.AddAsync(AuditLog.Create(
                command.WorkspaceId,
                command.TriggeredByUserId,
                "DataSource.SyncFailed",
                "DataSource",
                dataSource.Id.ToString(),
                $"Sync failed after {sw.ElapsedMilliseconds}ms: {ex.Message}"),
                cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Failure<SyncResultDto>(DataSourceErrors.ConnectionFailed(ex.Message));
        }
    }
}
