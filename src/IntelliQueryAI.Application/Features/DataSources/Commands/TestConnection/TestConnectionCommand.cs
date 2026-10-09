using System.Diagnostics;
using FluentValidation;
using IntelliQueryAI.Application.Abstractions.Messaging;
using IntelliQueryAI.Application.Features.DataSources.Dtos;
using IntelliQueryAI.Domain.Enums;
using IntelliQueryAI.Domain.Errors;
using IntelliQueryAI.Domain.Persistence;
using IntelliQueryAI.Domain.Services;
using IntelliQueryAI.SharedKernel.Results;

namespace IntelliQueryAI.Application.Features.DataSources.Commands.TestConnection;

/// <summary>
/// Command to test connectivity against an external database (either existing registered data source or raw connection params).
/// </summary>
public sealed record TestConnectionCommand(
    Guid? DataSourceId,
    DatabaseType? DatabaseType,
    string? ConnectionString) : ICommand<Result<TestConnectionResultDto>>;

public sealed class TestConnectionCommandValidator : AbstractValidator<TestConnectionCommand>
{
    public TestConnectionCommandValidator()
    {
        RuleFor(x => x)
            .Must(x => x.DataSourceId.HasValue || (!string.IsNullOrWhiteSpace(x.ConnectionString) && x.DatabaseType.HasValue))
            .WithMessage("Either DataSourceId or both DatabaseType and ConnectionString must be specified.");
    }
}

public sealed class TestConnectionCommandHandler : ICommandHandler<TestConnectionCommand, Result<TestConnectionResultDto>>
{
    private readonly IDataSourceStore _dataSourceStore;
    private readonly IEncryptionService _encryptionService;
    private readonly ISchemaDiscoveryEngine _discoveryEngine;

    public TestConnectionCommandHandler(
        IDataSourceStore dataSourceStore,
        IEncryptionService encryptionService,
        ISchemaDiscoveryEngine discoveryEngine)
    {
        _dataSourceStore = dataSourceStore;
        _encryptionService = encryptionService;
        _discoveryEngine = discoveryEngine;
    }

    public async Task<Result<TestConnectionResultDto>> HandleAsync(TestConnectionCommand command, CancellationToken cancellationToken = default)
    {
        DatabaseType dbType;
        string connStr;

        if (command.DataSourceId.HasValue)
        {
            var ds = await _dataSourceStore.GetByIdAsync(command.DataSourceId.Value, cancellationToken);
            if (ds is null)
            {
                return Result.Failure<TestConnectionResultDto>(DataSourceErrors.NotFound);
            }

            dbType = ds.DatabaseType;
            connStr = _encryptionService.Decrypt(ds.EncryptedConnectionString);
        }
        else
        {
            dbType = command.DatabaseType!.Value;
            connStr = command.ConnectionString!;
        }

        var sw = Stopwatch.StartNew();
        bool isSuccess = await _discoveryEngine.TestConnectionAsync(dbType, connStr, cancellationToken);
        sw.Stop();

        if (!isSuccess)
        {
            return new TestConnectionResultDto(false, "Connection attempt failed or timed out.", sw.ElapsedMilliseconds);
        }

        return new TestConnectionResultDto(true, "Successfully connected to target database.", sw.ElapsedMilliseconds);
    }
}
