using FluentValidation;
using IntelliQueryAI.Application.Abstractions.Messaging;
using IntelliQueryAI.Application.Features.BusinessKnowledge.Dtos;
using IntelliQueryAI.Domain.Entities.Audit;
using IntelliQueryAI.Domain.Entities.BusinessKnowledge;
using IntelliQueryAI.Domain.Errors;
using IntelliQueryAI.Domain.Persistence;
using IntelliQueryAI.SharedKernel.Results;

namespace IntelliQueryAI.Application.Features.BusinessKnowledge.Commands;

// -------------------------------------------------------------------------------------------------
// 1. Create Business Term
// -------------------------------------------------------------------------------------------------
public sealed record CreateBusinessTermCommand(
    Guid WorkspaceId,
    Guid UserId,
    string Name,
    string Definition,
    IReadOnlyList<string>? Aliases = null,
    string Category = "General",
    string Classification = "Internal",
    string? OwnerName = null,
    string? OwnerEmail = null,
    string? TargetTable = null,
    string? TargetColumn = null,
    string? SqlCalculationRule = null) : ICommand<Result<BusinessTermDto>>;

public sealed class CreateBusinessTermCommandValidator : AbstractValidator<CreateBusinessTermCommand>
{
    public CreateBusinessTermCommandValidator()
    {
        RuleFor(x => x.WorkspaceId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Definition).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.Category).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Classification).NotEmpty().MaximumLength(100);
    }
}

public sealed class CreateBusinessTermCommandHandler : ICommandHandler<CreateBusinessTermCommand, Result<BusinessTermDto>>
{
    private readonly IBusinessTermStore _store;
    private readonly IAuditLogStore _auditStore;

    public CreateBusinessTermCommandHandler(IBusinessTermStore store, IAuditLogStore auditStore)
    {
        _store = store;
        _auditStore = auditStore;
    }

    public async Task<Result<BusinessTermDto>> HandleAsync(CreateBusinessTermCommand command, CancellationToken cancellationToken = default)
    {
        var term = BusinessTerm.Create(
            command.WorkspaceId,
            command.Name,
            command.Definition,
            command.Aliases,
            command.Category,
            command.Classification,
            command.OwnerName,
            command.OwnerEmail,
            command.TargetTable,
            command.TargetColumn,
            command.SqlCalculationRule);

        await _store.AddAsync(term, cancellationToken);

        await _auditStore.AddAsync(AuditLog.Create(
            command.WorkspaceId,
            command.UserId,
            "BusinessKnowledge.CreateTerm",
            "BusinessTerm",
            term.Id.ToString(),
            $"Created business term '{term.Name}' ({term.Category})."), cancellationToken);

        return Result.Success(BusinessTermDto.FromDomain(term));
    }
}

// -------------------------------------------------------------------------------------------------
// 2. Update Business Term
// -------------------------------------------------------------------------------------------------
public sealed record UpdateBusinessTermCommand(
    Guid WorkspaceId,
    Guid UserId,
    Guid Id,
    string Name,
    string Definition,
    IReadOnlyList<string>? Aliases = null,
    string Category = "General",
    string Classification = "Internal",
    string? OwnerName = null,
    string? OwnerEmail = null,
    string? TargetTable = null,
    string? TargetColumn = null,
    string? SqlCalculationRule = null) : ICommand<Result<BusinessTermDto>>;

public sealed class UpdateBusinessTermCommandValidator : AbstractValidator<UpdateBusinessTermCommand>
{
    public UpdateBusinessTermCommandValidator()
    {
        RuleFor(x => x.WorkspaceId).NotEmpty();
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Definition).NotEmpty().MaximumLength(2000);
    }
}

public sealed class UpdateBusinessTermCommandHandler : ICommandHandler<UpdateBusinessTermCommand, Result<BusinessTermDto>>
{
    private readonly IBusinessTermStore _store;
    private readonly IAuditLogStore _auditStore;

    public UpdateBusinessTermCommandHandler(IBusinessTermStore store, IAuditLogStore auditStore)
    {
        _store = store;
        _auditStore = auditStore;
    }

    public async Task<Result<BusinessTermDto>> HandleAsync(UpdateBusinessTermCommand command, CancellationToken cancellationToken = default)
    {
        var term = await _store.GetByIdAsync(command.Id, cancellationToken);
        if (term is null || term.WorkspaceId != command.WorkspaceId)
        {
            return Result.Failure<BusinessTermDto>(Error.NotFound("BusinessTerm.NotFound", "The business term was not found in this workspace."));
        }

        term.Update(
            command.Name,
            command.Definition,
            command.Aliases,
            command.Category,
            command.Classification,
            command.OwnerName,
            command.OwnerEmail,
            command.TargetTable,
            command.TargetColumn,
            command.SqlCalculationRule);

        await _store.UpdateAsync(term, cancellationToken);

        await _auditStore.AddAsync(AuditLog.Create(
            command.WorkspaceId,
            command.UserId,
            "BusinessKnowledge.UpdateTerm",
            "BusinessTerm",
            term.Id.ToString(),
            $"Updated business term '{term.Name}'."), cancellationToken);

        return Result.Success(BusinessTermDto.FromDomain(term));
    }
}

// -------------------------------------------------------------------------------------------------
// 3. Delete Business Term
// -------------------------------------------------------------------------------------------------
public sealed record DeleteBusinessTermCommand(Guid WorkspaceId, Guid UserId, Guid Id) : ICommand<Result>;

public sealed class DeleteBusinessTermCommandHandler : ICommandHandler<DeleteBusinessTermCommand, Result>
{
    private readonly IBusinessTermStore _store;
    private readonly IAuditLogStore _auditStore;

    public DeleteBusinessTermCommandHandler(IBusinessTermStore store, IAuditLogStore auditStore)
    {
        _store = store;
        _auditStore = auditStore;
    }

    public async Task<Result> HandleAsync(DeleteBusinessTermCommand command, CancellationToken cancellationToken = default)
    {
        var term = await _store.GetByIdAsync(command.Id, cancellationToken);
        if (term is null || term.WorkspaceId != command.WorkspaceId)
        {
            return Result.Failure(Error.NotFound("BusinessTerm.NotFound", "The business term was not found in this workspace."));
        }

        await _store.DeleteAsync(term, cancellationToken);

        await _auditStore.AddAsync(AuditLog.Create(
            command.WorkspaceId,
            command.UserId,
            "BusinessKnowledge.DeleteTerm",
            "BusinessTerm",
            term.Id.ToString(),
            $"Deleted business term '{term.Name}'."), cancellationToken);

        return Result.Success();
    }
}

// -------------------------------------------------------------------------------------------------
// 4. Toggle Business Term Status
// -------------------------------------------------------------------------------------------------
public sealed record ToggleBusinessTermStatusCommand(Guid WorkspaceId, Guid UserId, Guid Id, bool IsActive) : ICommand<Result<BusinessTermDto>>;

public sealed class ToggleBusinessTermStatusCommandHandler : ICommandHandler<ToggleBusinessTermStatusCommand, Result<BusinessTermDto>>
{
    private readonly IBusinessTermStore _store;

    public ToggleBusinessTermStatusCommandHandler(IBusinessTermStore store)
    {
        _store = store;
    }

    public async Task<Result<BusinessTermDto>> HandleAsync(ToggleBusinessTermStatusCommand command, CancellationToken cancellationToken = default)
    {
        var term = await _store.GetByIdAsync(command.Id, cancellationToken);
        if (term is null || term.WorkspaceId != command.WorkspaceId)
        {
            return Result.Failure<BusinessTermDto>(Error.NotFound("BusinessTerm.NotFound", "The business term was not found in this workspace."));
        }

        term.SetActive(command.IsActive);
        await _store.UpdateAsync(term, cancellationToken);

        return Result.Success(BusinessTermDto.FromDomain(term));
    }
}

// -------------------------------------------------------------------------------------------------
// 5. Create Metric Definition
// -------------------------------------------------------------------------------------------------
public sealed record CreateMetricDefinitionCommand(
    Guid WorkspaceId,
    Guid UserId,
    string Name,
    string Description,
    string CalculationFormula,
    string Unit = "",
    Guid? DataSourceId = null,
    string? FilterExpression = null,
    string? TargetTable = null,
    string? TargetColumn = null,
    string AggregationType = "SUM",
    string TrendDirection = "HigherIsBetter") : ICommand<Result<MetricDefinitionDto>>;

public sealed class CreateMetricDefinitionCommandValidator : AbstractValidator<CreateMetricDefinitionCommand>
{
    public CreateMetricDefinitionCommandValidator()
    {
        RuleFor(x => x.WorkspaceId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.CalculationFormula).NotEmpty().MaximumLength(1000);
    }
}

public sealed class CreateMetricDefinitionCommandHandler : ICommandHandler<CreateMetricDefinitionCommand, Result<MetricDefinitionDto>>
{
    private readonly IMetricStore _store;
    private readonly IAuditLogStore _auditStore;

    public CreateMetricDefinitionCommandHandler(IMetricStore store, IAuditLogStore auditStore)
    {
        _store = store;
        _auditStore = auditStore;
    }

    public async Task<Result<MetricDefinitionDto>> HandleAsync(CreateMetricDefinitionCommand command, CancellationToken cancellationToken = default)
    {
        var metric = MetricDefinition.Create(
            command.WorkspaceId,
            command.Name,
            command.Description,
            command.CalculationFormula,
            command.Unit,
            command.DataSourceId,
            command.FilterExpression,
            command.TargetTable,
            command.TargetColumn,
            command.AggregationType,
            command.TrendDirection);

        await _store.AddAsync(metric, cancellationToken);

        await _auditStore.AddAsync(AuditLog.Create(
            command.WorkspaceId,
            command.UserId,
            "BusinessKnowledge.CreateMetric",
            "MetricDefinition",
            metric.Id.ToString(),
            $"Created metric '{metric.Name}' ({metric.AggregationType})."), cancellationToken);

        return Result.Success(MetricDefinitionDto.FromDomain(metric));
    }
}

// -------------------------------------------------------------------------------------------------
// 6. Update Metric Definition
// -------------------------------------------------------------------------------------------------
public sealed record UpdateMetricDefinitionCommand(
    Guid WorkspaceId,
    Guid UserId,
    Guid Id,
    string Name,
    string Description,
    string CalculationFormula,
    string Unit,
    Guid? DataSourceId,
    string? FilterExpression,
    string? TargetTable,
    string? TargetColumn,
    string AggregationType,
    string TrendDirection) : ICommand<Result<MetricDefinitionDto>>;

public sealed class UpdateMetricDefinitionCommandValidator : AbstractValidator<UpdateMetricDefinitionCommand>
{
    public UpdateMetricDefinitionCommandValidator()
    {
        RuleFor(x => x.WorkspaceId).NotEmpty();
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.CalculationFormula).NotEmpty().MaximumLength(1000);
    }
}

public sealed class UpdateMetricDefinitionCommandHandler : ICommandHandler<UpdateMetricDefinitionCommand, Result<MetricDefinitionDto>>
{
    private readonly IMetricStore _store;
    private readonly IAuditLogStore _auditStore;

    public UpdateMetricDefinitionCommandHandler(IMetricStore store, IAuditLogStore auditStore)
    {
        _store = store;
        _auditStore = auditStore;
    }

    public async Task<Result<MetricDefinitionDto>> HandleAsync(UpdateMetricDefinitionCommand command, CancellationToken cancellationToken = default)
    {
        var metric = await _store.GetByIdAsync(command.Id, cancellationToken);
        if (metric is null || metric.WorkspaceId != command.WorkspaceId)
        {
            return Result.Failure<MetricDefinitionDto>(Error.NotFound("MetricDefinition.NotFound", "The metric definition was not found in this workspace."));
        }

        metric.Update(
            command.Name,
            command.Description,
            command.CalculationFormula,
            command.Unit,
            command.DataSourceId,
            command.FilterExpression,
            command.TargetTable,
            command.TargetColumn,
            command.AggregationType,
            command.TrendDirection);

        await _store.UpdateAsync(metric, cancellationToken);

        await _auditStore.AddAsync(AuditLog.Create(
            command.WorkspaceId,
            command.UserId,
            "BusinessKnowledge.UpdateMetric",
            "MetricDefinition",
            metric.Id.ToString(),
            $"Updated metric '{metric.Name}'."), cancellationToken);

        return Result.Success(MetricDefinitionDto.FromDomain(metric));
    }
}

// -------------------------------------------------------------------------------------------------
// 7. Delete Metric Definition
// -------------------------------------------------------------------------------------------------
public sealed record DeleteMetricDefinitionCommand(Guid WorkspaceId, Guid UserId, Guid Id) : ICommand<Result>;

public sealed class DeleteMetricDefinitionCommandHandler : ICommandHandler<DeleteMetricDefinitionCommand, Result>
{
    private readonly IMetricStore _store;
    private readonly IAuditLogStore _auditStore;

    public DeleteMetricDefinitionCommandHandler(IMetricStore store, IAuditLogStore auditStore)
    {
        _store = store;
        _auditStore = auditStore;
    }

    public async Task<Result> HandleAsync(DeleteMetricDefinitionCommand command, CancellationToken cancellationToken = default)
    {
        var metric = await _store.GetByIdAsync(command.Id, cancellationToken);
        if (metric is null || metric.WorkspaceId != command.WorkspaceId)
        {
            return Result.Failure(Error.NotFound("MetricDefinition.NotFound", "The metric definition was not found in this workspace."));
        }

        await _store.DeleteAsync(metric, cancellationToken);

        await _auditStore.AddAsync(AuditLog.Create(
            command.WorkspaceId,
            command.UserId,
            "BusinessKnowledge.DeleteMetric",
            "MetricDefinition",
            metric.Id.ToString(),
            $"Deleted metric '{metric.Name}'."), cancellationToken);

        return Result.Success();
    }
}
