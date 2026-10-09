using IntelliQueryAI.AI.Coordinator;
using IntelliQueryAI.Application.Abstractions.Messaging;
using IntelliQueryAI.Application.Features.Scenarios.Commands;
using IntelliQueryAI.Application.Features.Scenarios.Dtos;
using IntelliQueryAI.Domain.Errors;
using IntelliQueryAI.Domain.Persistence;
using IntelliQueryAI.Domain.Services;
using IntelliQueryAI.SharedKernel.Results;

namespace IntelliQueryAI.Application.Features.Scenarios.Queries;

// 1. Get Scenarios by Workspace
public sealed record GetScenariosQuery(
    Guid WorkspaceId,
    int Limit = 50) : IQuery<Result<IReadOnlyList<ScenarioDto>>>;

public sealed class GetScenariosQueryHandler : IQueryHandler<GetScenariosQuery, Result<IReadOnlyList<ScenarioDto>>>
{
    private readonly IScenarioStore _scenarioStore;
    private readonly ITenantContext _tenantContext;

    public GetScenariosQueryHandler(
        IScenarioStore scenarioStore,
        ITenantContext tenantContext)
    {
        _scenarioStore = scenarioStore;
        _tenantContext = tenantContext;
    }

    public async Task<Result<IReadOnlyList<ScenarioDto>>> HandleAsync(
        GetScenariosQuery query,
        CancellationToken cancellationToken = default)
    {
        if (_tenantContext.WorkspaceId.HasValue && _tenantContext.WorkspaceId.Value != query.WorkspaceId)
        {
            return Result.Failure<IReadOnlyList<ScenarioDto>>(AiErrors.ContextTenantMismatch);
        }

        var scenarios = await _scenarioStore.GetByWorkspaceIdAsync(query.WorkspaceId, query.Limit, cancellationToken);
        var dtos = scenarios.Select(CreateScenarioCommandHandler.MapToDto).ToList();

        return Result.Success<IReadOnlyList<ScenarioDto>>(dtos);
    }
}

// 2. Get Scenario By Id
public sealed record GetScenarioByIdQuery(
    Guid WorkspaceId,
    Guid ScenarioId) : IQuery<Result<ScenarioDto>>;

public sealed class GetScenarioByIdQueryHandler : IQueryHandler<GetScenarioByIdQuery, Result<ScenarioDto>>
{
    private readonly IScenarioStore _scenarioStore;
    private readonly ITenantContext _tenantContext;

    public GetScenarioByIdQueryHandler(
        IScenarioStore scenarioStore,
        ITenantContext tenantContext)
    {
        _scenarioStore = scenarioStore;
        _tenantContext = tenantContext;
    }

    public async Task<Result<ScenarioDto>> HandleAsync(
        GetScenarioByIdQuery query,
        CancellationToken cancellationToken = default)
    {
        if (_tenantContext.WorkspaceId.HasValue && _tenantContext.WorkspaceId.Value != query.WorkspaceId)
        {
            return Result.Failure<ScenarioDto>(AiErrors.ContextTenantMismatch);
        }

        var scenario = await _scenarioStore.GetByIdAsync(query.ScenarioId, cancellationToken);
        if (scenario is null)
        {
            return Result.Failure<ScenarioDto>(AiErrors.ScenarioNotFound);
        }

        if (scenario.WorkspaceId != query.WorkspaceId)
        {
            return Result.Failure<ScenarioDto>(AiErrors.ScenarioTenantMismatch);
        }

        return Result.Success(CreateScenarioCommandHandler.MapToDto(scenario));
    }
}

// 3. Quick Simulate Scenario (Sandbox without saving)
public sealed record QuickSimulateScenarioQuery(
    Guid WorkspaceId,
    string TargetMetric,
    decimal BaselineValue,
    string TimeHorizon,
    IReadOnlyList<ScenarioVariableDto> Variables) : IQuery<Result<QuickSimulateResponseDto>>;

public sealed class QuickSimulateScenarioQueryHandler : IQueryHandler<QuickSimulateScenarioQuery, Result<QuickSimulateResponseDto>>
{
    private readonly IScenarioCoordinator _coordinator;
    private readonly ITenantContext _tenantContext;

    public QuickSimulateScenarioQueryHandler(
        IScenarioCoordinator coordinator,
        ITenantContext tenantContext)
    {
        _coordinator = coordinator;
        _tenantContext = tenantContext;
    }

    public async Task<Result<QuickSimulateResponseDto>> HandleAsync(
        QuickSimulateScenarioQuery query,
        CancellationToken cancellationToken = default)
    {
        if (_tenantContext.WorkspaceId.HasValue && _tenantContext.WorkspaceId.Value != query.WorkspaceId)
        {
            return Result.Failure<QuickSimulateResponseDto>(AiErrors.ContextTenantMismatch);
        }

        var domainVariables = query.Variables.Select(CreateScenarioCommandHandler.MapToDomain).ToList();

        var coordRequest = new CalculateScenarioRequest(
            query.WorkspaceId,
            Title: $"Simulation - {query.TargetMetric}",
            query.TargetMetric,
            query.BaselineValue,
            query.TimeHorizon,
            domainVariables);

        var coordResult = await _coordinator.CalculateAndAnalyzeAsync(coordRequest, cancellationToken);
        if (coordResult.IsFailure)
        {
            return Result.Failure<QuickSimulateResponseDto>(coordResult.Error);
        }

        var calc = coordResult.Value;

        var projections = calc.Simulation.Projections.Select(p => new ScenarioProjectionDto(
            p.CaseType,
            p.ProjectedValue,
            p.DeltaValue,
            p.DeltaPercentage,
            p.MonthlyBreakdown?.Select(m => new MonthlyDataPointDto(m.Month, m.Baseline, m.Projected, m.BestCase, m.WorstCase)).ToList())).ToList();

        var response = new QuickSimulateResponseDto(
            BaselineValue: calc.Simulation.BaselineValue,
            ExpectedValue: calc.Simulation.ExpectedValue,
            BestCaseValue: calc.Simulation.BestCaseValue,
            WorstCaseValue: calc.Simulation.WorstCaseValue,
            ExpectedDeltaValue: calc.Simulation.ExpectedDeltaValue,
            ExpectedDeltaPercentage: calc.Simulation.ExpectedDeltaPercentage,
            Projections: projections,
            AiExplanation: calc.SerializedNarrativeJson,
            ConfidenceScore: calc.Simulation.ConfidenceScore);

        return Result.Success(response);
    }
}
