using System.Text.Json;
using FluentValidation;
using IntelliQueryAI.AI.Coordinator;
using IntelliQueryAI.Application.Abstractions.Messaging;
using IntelliQueryAI.Application.Features.Investigations.Dtos;
using IntelliQueryAI.Domain.Entities.Investigations;
using IntelliQueryAI.Domain.Persistence;
using IntelliQueryAI.Domain.Services;
using IntelliQueryAI.SharedKernel.Results;
using Microsoft.Extensions.Logging;

namespace IntelliQueryAI.Application.Features.Investigations.Commands;

// ============================================================================
// 1. Start Investigation Command
// ============================================================================

public sealed record StartInvestigationCommand(
    string Question,
    Guid? DataSourceId = null,
    string? TargetMetric = null,
    Guid? WorkspaceId = null
) : ICommand<Result<InvestigationReportDto>>;

public sealed class StartInvestigationCommandValidator : AbstractValidator<StartInvestigationCommand>
{
    public StartInvestigationCommandValidator()
    {
        RuleFor(x => x.Question)
            .NotEmpty().WithMessage("Investigation question cannot be empty.")
            .MaximumLength(2000).WithMessage("Question must not exceed 2000 characters.");

        RuleFor(x => x.TargetMetric)
            .MaximumLength(200).When(x => !string.IsNullOrEmpty(x.TargetMetric));
    }
}

public sealed class StartInvestigationCommandHandler : ICommandHandler<StartInvestigationCommand, Result<InvestigationReportDto>>
{
    private readonly IInvestigationCoordinator _investigationCoordinator;
    private readonly IInvestigationReportStore _investigationReportStore;
    private readonly IWorkspaceStore _workspaceStore;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<StartInvestigationCommandHandler> _logger;

    public StartInvestigationCommandHandler(
        IInvestigationCoordinator investigationCoordinator,
        IInvestigationReportStore investigationReportStore,
        IWorkspaceStore workspaceStore,
        IUnitOfWork unitOfWork,
        ITenantContext tenantContext,
        ILogger<StartInvestigationCommandHandler> logger)
    {
        _investigationCoordinator = investigationCoordinator;
        _investigationReportStore = investigationReportStore;
        _workspaceStore = workspaceStore;
        _unitOfWork = unitOfWork;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<Result<InvestigationReportDto>> HandleAsync(
        StartInvestigationCommand command,
        CancellationToken cancellationToken)
    {
        var targetWorkspaceId = command.WorkspaceId ?? _tenantContext.WorkspaceId;
        if (!targetWorkspaceId.HasValue || targetWorkspaceId.Value == Guid.Empty)
        {
            return Result.Failure<InvestigationReportDto>(
                Error.Unauthorized("Tenant.Missing", "A valid workspace context is required."));
        }

        Guid workspaceGuid = targetWorkspaceId.Value;
        string workspaceName = "Your Workspace";
        var workspace = await _workspaceStore.GetByIdAsync(workspaceGuid, cancellationToken);
        if (workspace != null)
        {
            workspaceName = workspace.Name;
        }

        var coordinatorRequest = new InvestigationCoordinatorRequest(
            WorkspaceId: workspaceGuid,
            DataSourceId: command.DataSourceId,
            Question: command.Question.Trim(),
            TargetMetric: command.TargetMetric?.Trim(),
            WorkspaceName: workspaceName);

        var coordResult = await _investigationCoordinator.InvestigateAsync(coordinatorRequest, cancellationToken);
        if (coordResult.IsFailure)
        {
            return Result.Failure<InvestigationReportDto>(coordResult.Error);
        }

        var res = coordResult.Value;

        // Persist the completed report
        var report = InvestigationReport.Create(
            workspaceId: workspaceGuid,
            dataSourceId: command.DataSourceId,
            question: command.Question.Trim(),
            targetMetric: command.TargetMetric?.Trim(),
            summary: res.Summary,
            confidenceScore: res.ConfidenceScore,
            status: res.Status,
            rootCausesJson: JsonSerializer.Serialize(res.RootCauses),
            contributorsJson: JsonSerializer.Serialize(res.Contributors),
            evidenceJson: JsonSerializer.Serialize(res.Evidence),
            recommendationsJson: JsonSerializer.Serialize(res.Recommendations),
            queriesExecutedJson: JsonSerializer.Serialize(res.QueriesExecuted),
            followUpQuestionsJson: JsonSerializer.Serialize(res.FollowUpQuestions),
            durationMs: res.DurationMs,
            tokensUsed: res.TokensUsed);

        await _investigationReportStore.AddAsync(report, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Saved InvestigationReport '{Id}' for workspace '{WorkspaceId}'",
            report.Id, workspaceGuid);

        return Result.Success(InvestigationReportDto.FromDomain(report));
    }
}

// ============================================================================
// 2. Delete Investigation Command
// ============================================================================

public sealed record DeleteInvestigationCommand(
    Guid Id,
    Guid? WorkspaceId = null
) : ICommand<Result>;

public sealed class DeleteInvestigationCommandHandler : ICommandHandler<DeleteInvestigationCommand, Result>
{
    private readonly IInvestigationReportStore _investigationReportStore;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITenantContext _tenantContext;

    public DeleteInvestigationCommandHandler(
        IInvestigationReportStore investigationReportStore,
        IUnitOfWork unitOfWork,
        ITenantContext tenantContext)
    {
        _investigationReportStore = investigationReportStore;
        _unitOfWork = unitOfWork;
        _tenantContext = tenantContext;
    }

    public async Task<Result> HandleAsync(
        DeleteInvestigationCommand command,
        CancellationToken cancellationToken)
    {
        var targetWorkspaceId = command.WorkspaceId ?? _tenantContext.WorkspaceId;
        var report = await _investigationReportStore.GetByIdAsync(command.Id, cancellationToken);
        if (report == null || (targetWorkspaceId.HasValue && report.WorkspaceId != targetWorkspaceId.Value))
        {
            return Result.Failure(Error.NotFound("Investigation.NotFound", "Investigation report not found."));
        }

        await _investigationReportStore.DeleteAsync(report, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
