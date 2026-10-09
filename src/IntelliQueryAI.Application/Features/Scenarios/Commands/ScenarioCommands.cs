using System.Text.Json;
using FluentValidation;
using IntelliQueryAI.AI.Coordinator;
using IntelliQueryAI.Application.Abstractions.Messaging;
using IntelliQueryAI.Application.Features.Scenarios.Dtos;
using IntelliQueryAI.Domain.Entities.Audit;
using IntelliQueryAI.Domain.Entities.Scenarios;
using IntelliQueryAI.Domain.Errors;
using IntelliQueryAI.Domain.Persistence;
using IntelliQueryAI.Domain.Services;
using IntelliQueryAI.SharedKernel.Results;

namespace IntelliQueryAI.Application.Features.Scenarios.Commands;

// 1. Create Scenario
public sealed record CreateScenarioCommand(
    Guid WorkspaceId,
    Guid UserId,
    string Title,
    string? Description,
    string TargetMetric,
    decimal BaselineValue,
    string TimeHorizon,
    IReadOnlyList<ScenarioVariableDto> Variables) : ICommand<Result<ScenarioDto>>;

public sealed class CreateScenarioCommandValidator : AbstractValidator<CreateScenarioCommand>
{
    public CreateScenarioCommandValidator()
    {
        RuleFor(x => x.WorkspaceId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(250);
        RuleFor(x => x.TargetMetric).NotEmpty().MaximumLength(150);
        RuleFor(x => x.TimeHorizon).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Variables).NotEmpty().WithMessage("At least one scenario variable lever must be provided.");
    }
}

public sealed class CreateScenarioCommandHandler : ICommandHandler<CreateScenarioCommand, Result<ScenarioDto>>
{
    private readonly IScenarioCoordinator _coordinator;
    private readonly IScenarioStore _scenarioStore;
    private readonly IAuditLogStore _auditStore;
    private readonly ITenantContext _tenantContext;

    public CreateScenarioCommandHandler(
        IScenarioCoordinator coordinator,
        IScenarioStore scenarioStore,
        IAuditLogStore auditStore,
        ITenantContext tenantContext)
    {
        _coordinator = coordinator;
        _scenarioStore = scenarioStore;
        _auditStore = auditStore;
        _tenantContext = tenantContext;
    }

    public async Task<Result<ScenarioDto>> HandleAsync(
        CreateScenarioCommand command,
        CancellationToken cancellationToken = default)
    {
        if (_tenantContext.WorkspaceId.HasValue && _tenantContext.WorkspaceId.Value != command.WorkspaceId)
        {
            return Result.Failure<ScenarioDto>(AiErrors.ContextTenantMismatch);
        }

        var domainVariables = command.Variables.Select(MapToDomain).ToList();

        var coordRequest = new CalculateScenarioRequest(
            command.WorkspaceId,
            command.Title,
            command.TargetMetric,
            command.BaselineValue,
            command.TimeHorizon,
            domainVariables,
            command.Description);

        var coordResult = await _coordinator.CalculateAndAnalyzeAsync(coordRequest, cancellationToken);
        if (coordResult.IsFailure)
        {
            return Result.Failure<ScenarioDto>(coordResult.Error);
        }

        var calc = coordResult.Value;

        var scenario = Scenario.Create(
            command.WorkspaceId,
            command.UserId,
            command.Title,
            command.Description,
            command.TargetMetric,
            command.BaselineValue,
            command.TimeHorizon,
            domainVariables,
            calc.Simulation.Projections,
            calc.SerializedNarrativeJson,
            calc.Simulation.ConfidenceScore);

        await _scenarioStore.AddAsync(scenario, cancellationToken);

        await _auditStore.AddAsync(AuditLog.Create(
            command.WorkspaceId,
            command.UserId,
            "Scenario.Created",
            "Scenario",
            scenario.Id.ToString(),
            $"Created What-If scenario '{scenario.Title}' for metric '{scenario.TargetMetric}' over {scenario.TimeHorizon}."), cancellationToken);

        return Result.Success(MapToDto(scenario));
    }

    public static ScenarioVariable MapToDomain(ScenarioVariableDto dto) =>
        new(dto.Name, dto.Unit, dto.BaselineValue, dto.ChangePercentage, dto.MinBound, dto.MaxBound, dto.Weight);

    public static ScenarioDto MapToDto(Scenario s)
    {
        var variables = s.GetVariables().Select(v => new ScenarioVariableDto(
            v.Name, v.Unit, v.BaselineValue, v.ChangePercentage, v.MinBound, v.MaxBound, v.Weight)).ToList();

        var projections = s.GetProjections().Select(p => new ScenarioProjectionDto(
            p.CaseType,
            p.ProjectedValue,
            p.DeltaValue,
            p.DeltaPercentage,
            p.MonthlyBreakdown?.Select(m => new MonthlyDataPointDto(m.Month, m.Baseline, m.Projected, m.BestCase, m.WorstCase)).ToList())).ToList();

        return new ScenarioDto(
            s.Id,
            s.WorkspaceId,
            s.CreatedByUserId,
            s.Title,
            s.Description,
            s.TargetMetric,
            s.BaselineValue,
            s.TimeHorizon,
            variables,
            projections,
            s.AiExplanation,
            s.ConfidenceScore,
            s.CreatedAtUtc,
            s.UpdatedAtUtc);
    }
}

// 2. Rerun Scenario Simulation
public sealed record RerunScenarioSimulationCommand(
    Guid WorkspaceId,
    Guid UserId,
    Guid ScenarioId,
    IReadOnlyList<ScenarioVariableDto>? Variables = null,
    string? TimeHorizon = null) : ICommand<Result<ScenarioDto>>;

public sealed class RerunScenarioSimulationCommandHandler : ICommandHandler<RerunScenarioSimulationCommand, Result<ScenarioDto>>
{
    private readonly IScenarioCoordinator _coordinator;
    private readonly IScenarioStore _scenarioStore;
    private readonly IAuditLogStore _auditStore;
    private readonly ITenantContext _tenantContext;

    public RerunScenarioSimulationCommandHandler(
        IScenarioCoordinator coordinator,
        IScenarioStore scenarioStore,
        IAuditLogStore auditStore,
        ITenantContext tenantContext)
    {
        _coordinator = coordinator;
        _scenarioStore = scenarioStore;
        _auditStore = auditStore;
        _tenantContext = tenantContext;
    }

    public async Task<Result<ScenarioDto>> HandleAsync(
        RerunScenarioSimulationCommand command,
        CancellationToken cancellationToken = default)
    {
        if (_tenantContext.WorkspaceId.HasValue && _tenantContext.WorkspaceId.Value != command.WorkspaceId)
        {
            return Result.Failure<ScenarioDto>(AiErrors.ContextTenantMismatch);
        }

        var scenario = await _scenarioStore.GetByIdAsync(command.ScenarioId, cancellationToken);
        if (scenario is null)
        {
            return Result.Failure<ScenarioDto>(AiErrors.ScenarioNotFound);
        }

        if (scenario.WorkspaceId != command.WorkspaceId)
        {
            return Result.Failure<ScenarioDto>(AiErrors.ScenarioTenantMismatch);
        }

        var domainVariables = command.Variables != null && command.Variables.Count > 0
            ? command.Variables.Select(CreateScenarioCommandHandler.MapToDomain).ToList()
            : scenario.GetVariables();

        string horizon = !string.IsNullOrWhiteSpace(command.TimeHorizon)
            ? command.TimeHorizon
            : scenario.TimeHorizon;

        var coordRequest = new CalculateScenarioRequest(
            command.WorkspaceId,
            scenario.Title,
            scenario.TargetMetric,
            scenario.BaselineValue,
            horizon,
            domainVariables,
            scenario.Description);

        var coordResult = await _coordinator.CalculateAndAnalyzeAsync(coordRequest, cancellationToken);
        if (coordResult.IsFailure)
        {
            return Result.Failure<ScenarioDto>(coordResult.Error);
        }

        var calc = coordResult.Value;

        scenario.UpdateSimulationResults(
            domainVariables,
            calc.Simulation.Projections,
            calc.SerializedNarrativeJson,
            calc.Simulation.ConfidenceScore);

        await _scenarioStore.UpdateAsync(scenario, cancellationToken);

        await _auditStore.AddAsync(AuditLog.Create(
            command.WorkspaceId,
            command.UserId,
            "Scenario.Rerun",
            "Scenario",
            scenario.Id.ToString(),
            $"Recalibrated What-If scenario '{scenario.Title}' with updated variable levers."), cancellationToken);

        return Result.Success(CreateScenarioCommandHandler.MapToDto(scenario));
    }
}

// 3. Update Scenario Details
public sealed record UpdateScenarioCommand(
    Guid WorkspaceId,
    Guid UserId,
    Guid ScenarioId,
    string Title,
    string? Description) : ICommand<Result<ScenarioDto>>;

public sealed class UpdateScenarioCommandHandler : ICommandHandler<UpdateScenarioCommand, Result<ScenarioDto>>
{
    private readonly IScenarioStore _scenarioStore;
    private readonly IAuditLogStore _auditStore;
    private readonly ITenantContext _tenantContext;

    public UpdateScenarioCommandHandler(
        IScenarioStore scenarioStore,
        IAuditLogStore auditStore,
        ITenantContext tenantContext)
    {
        _scenarioStore = scenarioStore;
        _auditStore = auditStore;
        _tenantContext = tenantContext;
    }

    public async Task<Result<ScenarioDto>> HandleAsync(
        UpdateScenarioCommand command,
        CancellationToken cancellationToken = default)
    {
        if (_tenantContext.WorkspaceId.HasValue && _tenantContext.WorkspaceId.Value != command.WorkspaceId)
        {
            return Result.Failure<ScenarioDto>(AiErrors.ContextTenantMismatch);
        }

        var scenario = await _scenarioStore.GetByIdAsync(command.ScenarioId, cancellationToken);
        if (scenario is null)
        {
            return Result.Failure<ScenarioDto>(AiErrors.ScenarioNotFound);
        }

        if (scenario.WorkspaceId != command.WorkspaceId)
        {
            return Result.Failure<ScenarioDto>(AiErrors.ScenarioTenantMismatch);
        }

        scenario.UpdateDetails(command.Title, command.Description);
        await _scenarioStore.UpdateAsync(scenario, cancellationToken);

        await _auditStore.AddAsync(AuditLog.Create(
            command.WorkspaceId,
            command.UserId,
            "Scenario.Updated",
            "Scenario",
            scenario.Id.ToString(),
            $"Updated details for scenario '{scenario.Title}'."), cancellationToken);

        return Result.Success(CreateScenarioCommandHandler.MapToDto(scenario));
    }
}

// 4. Delete Scenario
public sealed record DeleteScenarioCommand(
    Guid WorkspaceId,
    Guid UserId,
    Guid ScenarioId) : ICommand<Result>;

public sealed class DeleteScenarioCommandHandler : ICommandHandler<DeleteScenarioCommand, Result>
{
    private readonly IScenarioStore _scenarioStore;
    private readonly IAuditLogStore _auditStore;
    private readonly ITenantContext _tenantContext;

    public DeleteScenarioCommandHandler(
        IScenarioStore scenarioStore,
        IAuditLogStore auditStore,
        ITenantContext tenantContext)
    {
        _scenarioStore = scenarioStore;
        _auditStore = auditStore;
        _tenantContext = tenantContext;
    }

    public async Task<Result> HandleAsync(
        DeleteScenarioCommand command,
        CancellationToken cancellationToken = default)
    {
        if (_tenantContext.WorkspaceId.HasValue && _tenantContext.WorkspaceId.Value != command.WorkspaceId)
        {
            return Result.Failure(AiErrors.ContextTenantMismatch);
        }

        var scenario = await _scenarioStore.GetByIdAsync(command.ScenarioId, cancellationToken);
        if (scenario is null)
        {
            return Result.Failure(AiErrors.ScenarioNotFound);
        }

        if (scenario.WorkspaceId != command.WorkspaceId)
        {
            return Result.Failure(AiErrors.ScenarioTenantMismatch);
        }

        await _scenarioStore.DeleteAsync(scenario, cancellationToken);

        await _auditStore.AddAsync(AuditLog.Create(
            command.WorkspaceId,
            command.UserId,
            "Scenario.Deleted",
            "Scenario",
            scenario.Id.ToString(),
            $"Deleted What-If scenario '{scenario.Title}'."), cancellationToken);

        return Result.Success();
    }
}
