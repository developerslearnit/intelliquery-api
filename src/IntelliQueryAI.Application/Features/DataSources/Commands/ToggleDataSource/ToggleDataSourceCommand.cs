using FluentValidation;
using IntelliQueryAI.Application.Abstractions.Messaging;
using IntelliQueryAI.Domain.Errors;
using IntelliQueryAI.Domain.Persistence;
using IntelliQueryAI.SharedKernel.Results;

namespace IntelliQueryAI.Application.Features.DataSources.Commands.ToggleDataSource;

public sealed record ToggleDataSourceCommand(
    Guid DataSourceId,
    bool Enable,
    Guid WorkspaceId) : ICommand<Result>;

public sealed class ToggleDataSourceCommandValidator : AbstractValidator<ToggleDataSourceCommand>
{
    public ToggleDataSourceCommandValidator()
    {
        RuleFor(x => x.DataSourceId).NotEmpty();
        RuleFor(x => x.WorkspaceId).NotEmpty();
    }
}

public sealed class ToggleDataSourceCommandHandler : ICommandHandler<ToggleDataSourceCommand, Result>
{
    private readonly IDataSourceStore _dataSourceStore;
    private readonly IUnitOfWork _unitOfWork;

    public ToggleDataSourceCommandHandler(IDataSourceStore dataSourceStore, IUnitOfWork unitOfWork)
    {
        _dataSourceStore = dataSourceStore;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> HandleAsync(ToggleDataSourceCommand command, CancellationToken cancellationToken = default)
    {
        var dataSource = await _dataSourceStore.GetByIdAsync(command.DataSourceId, cancellationToken);
        if (dataSource is null || dataSource.WorkspaceId != command.WorkspaceId)
        {
            return Result.Failure(DataSourceErrors.NotFound);
        }

        if (command.Enable)
        {
            dataSource.Enable();
        }
        else
        {
            dataSource.Disable();
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
