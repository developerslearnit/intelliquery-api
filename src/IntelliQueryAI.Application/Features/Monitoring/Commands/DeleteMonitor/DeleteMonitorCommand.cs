using FluentValidation;
using IntelliQueryAI.Application.Abstractions.Messaging;
using IntelliQueryAI.Domain.Entities.Audit;
using IntelliQueryAI.Domain.Errors;
using IntelliQueryAI.Domain.Persistence;
using IntelliQueryAI.SharedKernel.Results;

namespace IntelliQueryAI.Application.Features.Monitoring.Commands.DeleteMonitor;

public sealed record DeleteMonitorCommand(
    Guid WorkspaceId,
    Guid MonitorId,
    Guid UserId) : ICommand<Result>;

public sealed class DeleteMonitorCommandValidator : AbstractValidator<DeleteMonitorCommand>
{
    public DeleteMonitorCommandValidator()
    {
        RuleFor(x => x.WorkspaceId).NotEmpty();
        RuleFor(x => x.MonitorId).NotEmpty();
    }
}

public sealed class DeleteMonitorCommandHandler : ICommandHandler<DeleteMonitorCommand, Result>
{
    private readonly IMonitorStore _monitorStore;
    private readonly IAuditLogStore _auditLogStore;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteMonitorCommandHandler(
        IMonitorStore monitorStore,
        IAuditLogStore auditLogStore,
        IUnitOfWork unitOfWork)
    {
        _monitorStore = monitorStore;
        _auditLogStore = auditLogStore;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> HandleAsync(DeleteMonitorCommand command, CancellationToken cancellationToken = default)
    {
        var monitor = await _monitorStore.GetByIdAsync(command.MonitorId, cancellationToken);
        if (monitor is null || monitor.WorkspaceId != command.WorkspaceId)
        {
            return Result.Failure(MonitoringErrors.MonitorNotFound);
        }

        await _monitorStore.DeleteAsync(monitor, cancellationToken);

        await _auditLogStore.AddAsync(AuditLog.Create(
            command.WorkspaceId,
            command.UserId,
            "Monitoring.Delete",
            "Monitor",
            command.MonitorId.ToString(),
            $"Deleted AI Monitor '{monitor.Name}'."),
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
