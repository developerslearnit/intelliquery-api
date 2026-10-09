using System.Diagnostics;
using System.Text.Json;
using FluentValidation;
using IntelliQueryAI.AI.Coordinator;
using IntelliQueryAI.Application.Abstractions.Messaging;
using IntelliQueryAI.Application.Features.Monitoring.Dtos;
using IntelliQueryAI.Domain.Entities.Alerts;
using IntelliQueryAI.Domain.Entities.Audit;
using IntelliQueryAI.Domain.Entities.Monitoring;
using IntelliQueryAI.Domain.Enums;
using IntelliQueryAI.Domain.Errors;
using IntelliQueryAI.Domain.Persistence;
using IntelliQueryAI.Domain.Services;
using IntelliQueryAI.SharedKernel.Results;

namespace IntelliQueryAI.Application.Features.Monitoring.Commands.RunMonitor;

public sealed record RunMonitorCommand(
    Guid WorkspaceId,
    Guid MonitorId,
    Guid UserId) : ICommand<Result<RunMonitorResponseDto>>;

public sealed class RunMonitorCommandValidator : AbstractValidator<RunMonitorCommand>
{
    public RunMonitorCommandValidator()
    {
        RuleFor(x => x.WorkspaceId).NotEmpty();
        RuleFor(x => x.MonitorId).NotEmpty();
    }
}

public sealed class RunMonitorCommandHandler : ICommandHandler<RunMonitorCommand, Result<RunMonitorResponseDto>>
{
    private readonly IMonitorStore _monitorStore;
    private readonly IAlertStore _alertStore;
    private readonly IDataSourceStore _dataSourceStore;
    private readonly IExecutiveCoordinator _executiveCoordinator;
    private readonly ISafeQueryExecutor _queryExecutor;
    private readonly IEncryptionService _encryptionService;
    private readonly IAuditLogStore _auditLogStore;
    private readonly IUnitOfWork _unitOfWork;

    public RunMonitorCommandHandler(
        IMonitorStore monitorStore,
        IAlertStore alertStore,
        IDataSourceStore dataSourceStore,
        IExecutiveCoordinator executiveCoordinator,
        ISafeQueryExecutor queryExecutor,
        IEncryptionService encryptionService,
        IAuditLogStore auditLogStore,
        IUnitOfWork unitOfWork)
    {
        _monitorStore = monitorStore;
        _alertStore = alertStore;
        _dataSourceStore = dataSourceStore;
        _executiveCoordinator = executiveCoordinator;
        _queryExecutor = queryExecutor;
        _encryptionService = encryptionService;
        _auditLogStore = auditLogStore;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<RunMonitorResponseDto>> HandleAsync(
        RunMonitorCommand command,
        CancellationToken cancellationToken = default)
    {
        var monitor = await _monitorStore.GetByIdAsync(command.MonitorId, cancellationToken);
        if (monitor is null || monitor.WorkspaceId != command.WorkspaceId)
        {
            return Result.Failure<RunMonitorResponseDto>(MonitoringErrors.MonitorNotFound);
        }

        var stopwatch = Stopwatch.StartNew();
        var execution = MonitorExecution.Start(monitor.Id);
        await _monitorStore.AddExecutionAsync(execution, cancellationToken);

        decimal currentMetricValue = 100m;
        decimal baselineMetricValue = 100m;

        if (!string.IsNullOrWhiteSpace(monitor.QueryTemplate))
        {
            var dataSource = await _dataSourceStore.GetByIdAsync(monitor.DataSourceId, cancellationToken);
            if (dataSource is not null && dataSource.IsActive)
            {
                var decryptedConn = _encryptionService.Decrypt(dataSource.EncryptedConnectionString);
                var queryResult = await _queryExecutor.ExecuteSafeQueryAsync(
                    dataSource.DatabaseType,
                    decryptedConn,
                    monitor.QueryTemplate,
                    15,
                    cancellationToken);

                if (queryResult.IsSuccess && queryResult.Rows.Count > 0)
                {
                    var firstRow = queryResult.Rows[0];
                    var firstVal = firstRow.Values.FirstOrDefault();
                    if (firstVal is not null && decimal.TryParse(firstVal.ToString(), out var parsedVal))
                    {
                        currentMetricValue = parsedVal;
                    }
                }
            }
        }

        bool ruleBreached = false;
        foreach (var rule in monitor.Rules)
        {
            if (rule.Evaluate(currentMetricValue, baselineMetricValue))
            {
                ruleBreached = true;
                break;
            }
        }

        var evalReq = new EvaluateMonitorRequest(
            command.WorkspaceId,
            monitor.Id,
            monitor.Name,
            monitor.MonitorType,
            currentMetricValue,
            baselineMetricValue,
            new List<decimal> { baselineMetricValue, currentMetricValue });

        var evalRes = await _executiveCoordinator.EvaluateMonitorAsync(evalReq, cancellationToken);
        stopwatch.Stop();

        bool alertTriggered = ruleBreached || (evalRes.IsSuccess && evalRes.Value.TriggerAlert);
        var findings = evalRes.IsSuccess ? evalRes.Value.Findings : "On-demand execution completed.";
        var insights = evalRes.IsSuccess
            ? JsonSerializer.Serialize(new
            {
                evalRes.Value.TrendSummary,
                evalRes.Value.Recommendations
            })
            : null;

        execution.Complete(
            currentMetricValue,
            baselineMetricValue,
            findings,
            insights,
            alertTriggered,
            stopwatch.ElapsedMilliseconds);

        if (alertTriggered)
        {
            var severity = evalRes.IsSuccess ? evalRes.Value.Severity : AlertSeverity.Warning;
            var alert = Alert.Create(
                command.WorkspaceId,
                monitor.Id,
                AlertType.ThresholdExceeded,
                severity,
                $"Triggered: {monitor.Name}",
                findings,
                currentMetricValue,
                baselineMetricValue);

            await _alertStore.AddAsync(alert, cancellationToken);
        }

        await _auditLogStore.AddAsync(AuditLog.Create(
            command.WorkspaceId,
            command.UserId,
            "Monitoring.RunManual",
            "Monitor",
            monitor.Id.ToString(),
            $"Executed monitor '{monitor.Name}'. Alert={alertTriggered} in {stopwatch.ElapsedMilliseconds}ms."),
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new RunMonitorResponseDto(
            execution.Id,
            monitor.Id,
            execution.Status.ToString(),
            currentMetricValue,
            baselineMetricValue,
            findings,
            alertTriggered,
            stopwatch.ElapsedMilliseconds));
    }
}
