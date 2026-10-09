using System.Diagnostics;
using FluentValidation;
using IntelliQueryAI.Application.Abstractions.Messaging;
using IntelliQueryAI.Application.Features.DataSources.Dtos;
using IntelliQueryAI.Domain.Entities.Audit;
using IntelliQueryAI.Domain.Entities.DataSources;
using IntelliQueryAI.Domain.Enums;
using IntelliQueryAI.Domain.Errors;
using IntelliQueryAI.Domain.Persistence;
using IntelliQueryAI.Domain.Services;
using IntelliQueryAI.SharedKernel.Results;

using Microsoft.Extensions.Logging;

namespace IntelliQueryAI.Application.Features.DataSources.Commands.RegisterDataSource;

/// <summary>
/// Command to register a new external database connection for a workspace.
/// Validates connectivity, enforces subscription limits, and stores encrypted credentials.
/// </summary>
public sealed record RegisterDataSourceCommand(
    Guid WorkspaceId,
    string Name,
    DatabaseType DatabaseType,
    string ConnectionString,
    Guid UserId) : ICommand<Result<DataSourceDto>>;

public sealed class RegisterDataSourceCommandValidator : AbstractValidator<RegisterDataSourceCommand>
{
    public RegisterDataSourceCommandValidator()
    {
        RuleFor(x => x.WorkspaceId).NotEmpty().WithMessage("Workspace identifier is required.");
        RuleFor(x => x.Name).NotEmpty().WithMessage("Data source name is required.");
        RuleFor(x => x.ConnectionString).NotEmpty().WithMessage("Connection string is required.");
        RuleFor(x => x.UserId).NotEmpty().WithMessage("User identifier is required.");
    }
}

public sealed class RegisterDataSourceCommandHandler : ICommandHandler<RegisterDataSourceCommand, Result<DataSourceDto>>
{
    private readonly IDataSourceStore _dataSourceStore;
    private readonly ISubscriptionStore _subscriptionStore;
    private readonly IEncryptionService _encryptionService;
    private readonly ISchemaDiscoveryEngine _discoveryEngine;
    private readonly IAuditLogStore _auditLogStore;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISchemaMetadataStore? _schemaMetadataStore;
    private readonly IntelliQueryAI.AI.Embeddings.ISchemaEmbeddingIndexer? _embeddingIndexer;
    private readonly ILogger<RegisterDataSourceCommandHandler>? _logger;

    public RegisterDataSourceCommandHandler(
        IDataSourceStore dataSourceStore,
        ISubscriptionStore subscriptionStore,
        IEncryptionService encryptionService,
        ISchemaDiscoveryEngine discoveryEngine,
        IAuditLogStore auditLogStore,
        IUnitOfWork unitOfWork,
        ISchemaMetadataStore? schemaMetadataStore = null,
        IntelliQueryAI.AI.Embeddings.ISchemaEmbeddingIndexer? embeddingIndexer = null,
        Microsoft.Extensions.Logging.ILogger<RegisterDataSourceCommandHandler>? logger = null)
    {
        _dataSourceStore = dataSourceStore;
        _subscriptionStore = subscriptionStore;
        _encryptionService = encryptionService;
        _discoveryEngine = discoveryEngine;
        _auditLogStore = auditLogStore;
        _unitOfWork = unitOfWork;
        _schemaMetadataStore = schemaMetadataStore;
        _embeddingIndexer = embeddingIndexer;
        _logger = logger;
    }

    public async Task<Result<DataSourceDto>> HandleAsync(RegisterDataSourceCommand command, CancellationToken cancellationToken = default)
    {
        // 1. Enforce subscription tier limits
        var subscription = await _subscriptionStore.GetByWorkspaceIdAsync(command.WorkspaceId, cancellationToken);
        if (subscription is not null)
        {
            int currentCount = await _dataSourceStore.CountByWorkspaceIdAsync(command.WorkspaceId, cancellationToken);
            if (currentCount >= subscription.MaxDataSources)
            {
                return Result.Failure<DataSourceDto>(SubscriptionErrors.DataSourceLimitReached);
            }
        }

        // 2. Validate live connectivity to target database
        bool isConnected = await _discoveryEngine.TestConnectionAsync(command.DatabaseType, command.ConnectionString, cancellationToken);
        if (!isConnected)
        {
            return Result.Failure<DataSourceDto>(DataSourceErrors.ConnectionFailed("Could not connect to target database with provided parameters."));
        }

        // 3. Encrypt connection string with AES-256 GCM
        string encryptedConn = _encryptionService.Encrypt(command.ConnectionString);

        // 4. Create and persist aggregate
        var createResult = DataSource.Create(
            command.WorkspaceId,
            command.Name,
            command.DatabaseType,
            encryptedConn);

        if (createResult.IsFailure)
        {
            return Result.Failure<DataSourceDto>(createResult.Error);
        }

        var dataSource = createResult.Value;
        await _dataSourceStore.AddAsync(dataSource, cancellationToken);

        // 5. Audit log
        await _auditLogStore.AddAsync(AuditLog.Create(
            command.WorkspaceId,
            command.UserId,
            "DataSource.Register",
            "DataSource",
            dataSource.Id.ToString(),
            $"Registered {command.DatabaseType} data source '{dataSource.Name}'."),
            cancellationToken);

        // 6. Automated initial schema discovery & PgVector indexing
        if (_schemaMetadataStore != null)
        {
            dataSource.MarkSyncStarted();
            try
            {
                var discovery = await _discoveryEngine.DiscoverSchemaAsync(
                    command.WorkspaceId,
                    dataSource.Id,
                    command.DatabaseType,
                    command.ConnectionString,
                    cancellationToken);

                await _schemaMetadataStore.ReplaceSchemaAsync(
                    command.WorkspaceId,
                    dataSource.Id,
                    discovery.Columns,
                    discovery.Relationships,
                    discovery.Routines,
                    cancellationToken);

                if (_embeddingIndexer != null)
                {
                    await _embeddingIndexer.IndexSchemaAsync(
                        command.WorkspaceId,
                        dataSource.Id,
                        discovery.Columns,
                        discovery.Relationships,
                        cancellationToken);
                }

                dataSource.MarkSyncCompleted();
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Initial automated schema discovery failed for data source {Name}: {Message}", dataSource.Name, ex.Message);
                dataSource.MarkSyncFailed(ex.Message);
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new DataSourceDto(
            dataSource.Id,
            dataSource.WorkspaceId,
            dataSource.Name,
            dataSource.DatabaseType.ToString(),
            dataSource.IsActive,
            dataSource.LastSyncAt,
            dataSource.SyncStatus.ToString(),
            dataSource.SyncErrorMessage,
            dataSource.CreatedAtUtc);
    }
}
