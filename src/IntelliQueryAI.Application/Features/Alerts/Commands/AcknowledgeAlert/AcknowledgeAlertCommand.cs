using FluentValidation;
using IntelliQueryAI.Application.Abstractions.Messaging;
using IntelliQueryAI.Application.Features.Alerts.Dtos;
using IntelliQueryAI.Domain.Entities.Audit;
using IntelliQueryAI.Domain.Errors;
using IntelliQueryAI.Domain.Persistence;
using IntelliQueryAI.SharedKernel.Results;

namespace IntelliQueryAI.Application.Features.Alerts.Commands.AcknowledgeAlert;

public sealed record AcknowledgeAlertCommand(
    Guid WorkspaceId,
    Guid AlertId,
    Guid UserId) : ICommand<Result<AlertDto>>;

public sealed class AcknowledgeAlertCommandValidator : AbstractValidator<AcknowledgeAlertCommand>
{
    public AcknowledgeAlertCommandValidator()
    {
        RuleFor(x => x.WorkspaceId).NotEmpty();
        RuleFor(x => x.AlertId).NotEmpty();
        RuleFor(x => x.UserId).NotEmpty();
    }
}

public sealed class AcknowledgeAlertCommandHandler : ICommandHandler<AcknowledgeAlertCommand, Result<AlertDto>>
{
    private readonly IAlertStore _alertStore;
    private readonly IAuditLogStore _auditLogStore;
    private readonly IUnitOfWork _unitOfWork;

    public AcknowledgeAlertCommandHandler(
        IAlertStore alertStore,
        IAuditLogStore auditLogStore,
        IUnitOfWork unitOfWork)
    {
        _alertStore = alertStore;
        _auditLogStore = auditLogStore;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<AlertDto>> HandleAsync(
        AcknowledgeAlertCommand command,
        CancellationToken cancellationToken = default)
    {
        var alert = await _alertStore.GetByIdAsync(command.AlertId, cancellationToken);
        if (alert is null || alert.WorkspaceId != command.WorkspaceId)
        {
            return Result.Failure<AlertDto>(MonitoringErrors.AlertNotFound);
        }

        if (alert.IsAcknowledged)
        {
            return Result.Failure<AlertDto>(MonitoringErrors.AlertAlreadyAcknowledged);
        }

        alert.Acknowledge(command.UserId);
        await _alertStore.UpdateAsync(alert, cancellationToken);

        await _auditLogStore.AddAsync(AuditLog.Create(
            command.WorkspaceId,
            command.UserId,
            "Alerts.Acknowledge",
            "Alert",
            alert.Id.ToString(),
            $"Acknowledged alert '{alert.Title}' (Severity: {alert.Severity})."),
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var dto = new AlertDto(
            alert.Id,
            alert.WorkspaceId,
            alert.MonitorId,
            alert.AlertType.ToString(),
            alert.Severity.ToString(),
            alert.Title,
            alert.Description,
            alert.MetricValue,
            alert.ThresholdValue,
            alert.IsAcknowledged,
            alert.AcknowledgedAtUtc,
            alert.AcknowledgedByUserId,
            alert.CreatedAtUtc);

        return Result.Success(dto);
    }
}
