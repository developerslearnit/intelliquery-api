using FluentValidation;
using IntelliQueryAI.AI.Coordinator;
using IntelliQueryAI.Application.Abstractions.Messaging;
using IntelliQueryAI.Application.Features.Ai.Dtos;
using IntelliQueryAI.Domain.Entities.Audit;
using IntelliQueryAI.Domain.Errors;
using IntelliQueryAI.Domain.Persistence;
using IntelliQueryAI.SharedKernel.Results;

namespace IntelliQueryAI.Application.Features.Ai.Commands.ExecuteChatQuery;

public sealed record ExecuteChatQueryCommand(
    Guid WorkspaceId,
    Guid DataSourceId,
    Guid UserId,
    string Question,
    Guid? SessionId = null,
    AiContextDto? Context = null,
    string? UserRole = null) : ICommand<Result<ChatQueryResponseDto>>;

public sealed class ExecuteChatQueryCommandValidator : AbstractValidator<ExecuteChatQueryCommand>
{
    public ExecuteChatQueryCommandValidator()
    {
        RuleFor(x => x.WorkspaceId).NotEmpty().WithMessage("Workspace identifier is required.");
        RuleFor(x => x.DataSourceId).NotEmpty().WithMessage("Data source identifier is required.");
        RuleFor(x => x.Question).NotEmpty().WithMessage("Analytical question cannot be empty.");
        RuleFor(x => x.Context)
            .Must((cmd, ctx) => ctx == null || ctx.WorkspaceId == cmd.WorkspaceId)
            .WithMessage("Context workspace identifier must match the executing workspace.");
    }
}

public sealed class ExecuteChatQueryCommandHandler : ICommandHandler<ExecuteChatQueryCommand, Result<ChatQueryResponseDto>>
{
    private readonly IAgentCoordinator _coordinator;
    private readonly ISubscriptionStore _subscriptionStore;
    private readonly IAuditLogStore _auditLogStore;
    private readonly IUnitOfWork _unitOfWork;

    public ExecuteChatQueryCommandHandler(
        IAgentCoordinator coordinator,
        ISubscriptionStore subscriptionStore,
        IAuditLogStore auditLogStore,
        IUnitOfWork unitOfWork)
    {
        _coordinator = coordinator;
        _subscriptionStore = subscriptionStore;
        _auditLogStore = auditLogStore;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<ChatQueryResponseDto>> HandleAsync(
        ExecuteChatQueryCommand command,
        CancellationToken cancellationToken = default)
    {
        var subscription = await _subscriptionStore.GetByWorkspaceIdAsync(command.WorkspaceId, cancellationToken);
        if (subscription is not null && !subscription.CanExecuteQuery())
        {
            return Result.Failure<ChatQueryResponseDto>(SubscriptionErrors.QuotaExceeded);
        }

        string? contextPrompt = null;
        if (command.Context != null)
        {
            var metaParts = command.Context.AdditionalMetadata != null && command.Context.AdditionalMetadata.Count > 0
                ? string.Join(", ", command.Context.AdditionalMetadata.Select(kv => $"{kv.Key}={kv.Value}"))
                : string.Empty;

            contextPrompt = $"Context: [{command.Context.ContextType.ToUpperInvariant()}] {command.Context.EntityTitle ?? command.Context.Metric}" +
                (!string.IsNullOrWhiteSpace(command.Context.Metric) ? $" | Target Metric: {command.Context.Metric}" : "") +
                (!string.IsNullOrWhiteSpace(command.Context.DateRange) ? $" | Date Range: {command.Context.DateRange}" : "") +
                (!string.IsNullOrWhiteSpace(metaParts) ? $" | Attributes: {metaParts}" : "");
        }

        var coordRequest = new CoordinatorQueryRequest(
            command.WorkspaceId,
            command.DataSourceId,
            command.UserId,
            command.Question,
            command.SessionId,
            contextPrompt,
            command.UserRole);

        var coordResult = await _coordinator.ExecuteQueryPipelineAsync(coordRequest, cancellationToken);
        if (coordResult.IsFailure)
        {
            return Result.Failure<ChatQueryResponseDto>(coordResult.Error);
        }

        var res = coordResult.Value;

        if (subscription is not null)
        {
            subscription.IncrementQueryUsage();
        }

        // Record audit entry
        await _auditLogStore.AddAsync(AuditLog.Create(
            command.WorkspaceId,
            command.UserId,
            "AI.ExecuteQuery",
            "ChatQuery",
            command.DataSourceId.ToString(),
            $"Executed AI analytical query: '{command.Question.Take(80)}...' in {res.TotalDurationMs}ms."),
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var chartDto = new ChartDto(
            res.RecommendedChart.RecommendedChart.ToString(),
            res.RecommendedChart.Title,
            res.RecommendedChart.XAxisColumn,
            res.RecommendedChart.YAxisColumns,
            res.RecommendedChart.Description,
            res.RecommendedChart.StylingOptions);

        var dto = new ChatQueryResponseDto(
            res.Question,
            res.GeneratedSql,
            res.Results.Columns,
            res.Results.Rows,
            res.Results.RowCount,
            res.Results.ExecutionDurationMs,
            res.Insights,
            chartDto,
            res.ConfidenceScore,
            res.TotalDurationMs,
            res.TotalTokensUsed);

        return Result.Success(dto);
    }
}
