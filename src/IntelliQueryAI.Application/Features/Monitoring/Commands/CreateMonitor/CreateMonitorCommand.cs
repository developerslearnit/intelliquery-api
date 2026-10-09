using FluentValidation;
using IntelliQueryAI.Application.Abstractions.Messaging;
using IntelliQueryAI.Application.Features.Monitoring.Dtos;
using IntelliQueryAI.Domain.Entities.Audit;
using IntelliQueryAI.Domain.Entities.Monitoring;
using IntelliQueryAI.Domain.Enums;
using IntelliQueryAI.Domain.Errors;
using IntelliQueryAI.Domain.Persistence;
using IntelliQueryAI.SharedKernel.Results;
using Monitor = IntelliQueryAI.Domain.Entities.Monitoring.Monitor;

namespace IntelliQueryAI.Application.Features.Monitoring.Commands.CreateMonitor;

public sealed record CreateMonitorCommand(
    Guid WorkspaceId,
    Guid UserId,
    Guid DataSourceId,
    string Name,
    string? Description,
    MonitorType MonitorType,
    ScheduleType ScheduleType,
    string? QueryTemplate,
    string? TargetMetric,
    IReadOnlyList<CreateMonitorRuleRequest>? Rules) : ICommand<Result<MonitorDto>>;

public sealed class CreateMonitorCommandValidator : AbstractValidator<CreateMonitorCommand>
{
    public CreateMonitorCommandValidator()
    {
        RuleFor(x => x.WorkspaceId).NotEmpty().WithMessage("Workspace identifier is required.");
        RuleFor(x => x.DataSourceId).NotEmpty().WithMessage("Data source identifier is required.");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200).WithMessage("Monitor name must not be empty or exceed 200 characters.");
    }
}

public sealed class CreateMonitorCommandHandler : ICommandHandler<CreateMonitorCommand, Result<MonitorDto>>
{
    private readonly IMonitorStore _monitorStore;
    private readonly IDataSourceStore _dataSourceStore;
    private readonly IAuditLogStore _auditLogStore;
    private readonly IUnitOfWork _unitOfWork;

    public CreateMonitorCommandHandler(
        IMonitorStore monitorStore,
        IDataSourceStore dataSourceStore,
        IAuditLogStore auditLogStore,
        IUnitOfWork unitOfWork)
    {
        _monitorStore = monitorStore;
        _dataSourceStore = dataSourceStore;
        _auditLogStore = auditLogStore;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<MonitorDto>> HandleAsync(
        CreateMonitorCommand command,
        CancellationToken cancellationToken = default)
    {
        var dataSource = await _dataSourceStore.GetByIdAsync(command.DataSourceId, cancellationToken);
        if (dataSource is null || dataSource.WorkspaceId != command.WorkspaceId)
        {
            return Result.Failure<MonitorDto>(MonitoringErrors.InvalidDataSource);
        }

        var monitor = Monitor.Create(
            command.WorkspaceId,
            command.DataSourceId,
            command.Name,
            command.Description,
            command.MonitorType,
            command.ScheduleType,
            command.QueryTemplate,
            command.TargetMetric);

        if (command.Rules is not null && command.Rules.Count > 0)
        {
            foreach (var ruleReq in command.Rules)
            {
                var rule = MonitorRule.Create(
                    monitor.Id,
                    ruleReq.RuleType,
                    ruleReq.ThresholdValue,
                    ruleReq.ComparisonOperator,
                    ruleReq.TimeWindow);

                monitor.AddRule(rule);
            }
        }
        else
        {
            // Default threshold rule (e.g. alert if metric drops below 0 or surges 20%)
            var defaultRule = MonitorRule.Create(
                monitor.Id,
                RuleType.PercentageChange,
                20.0m,
                ComparisonOperator.GreaterThanOrEqual,
                "30d");
            monitor.AddRule(defaultRule);
        }

        await _monitorStore.AddAsync(monitor, cancellationToken);

        await _auditLogStore.AddAsync(AuditLog.Create(
            command.WorkspaceId,
            command.UserId,
            "Monitoring.Create",
            "Monitor",
            monitor.Id.ToString(),
            $"Created AI Monitor '{monitor.Name}' ({monitor.MonitorType}) with {monitor.Rules.Count} rules."),
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var ruleDtos = monitor.Rules.Select(r => new MonitorRuleDto(
            r.Id,
            r.MonitorId,
            r.RuleType.ToString(),
            r.ThresholdValue,
            r.ComparisonOperator.ToString(),
            r.TimeWindow,
            r.CreatedAtUtc)).ToList();

        var dto = new MonitorDto(
            monitor.Id,
            monitor.WorkspaceId,
            monitor.DataSourceId,
            monitor.Name,
            monitor.Description,
            monitor.MonitorType.ToString(),
            monitor.IsActive,
            monitor.ScheduleType.ToString(),
            monitor.QueryTemplate,
            monitor.TargetMetric,
            monitor.LastExecutedAtUtc,
            monitor.CreatedAtUtc,
            ruleDtos,
            Array.Empty<MonitorExecutionDto>());

        return Result.Success(dto);
    }
}
