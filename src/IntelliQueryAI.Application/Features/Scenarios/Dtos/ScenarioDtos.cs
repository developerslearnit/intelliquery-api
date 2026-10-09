namespace IntelliQueryAI.Application.Features.Scenarios.Dtos;

public sealed record ScenarioVariableDto(
    string Name,
    string Unit,
    decimal BaselineValue,
    decimal ChangePercentage,
    decimal? MinBound = null,
    decimal? MaxBound = null,
    double Weight = 1.0);

public sealed record MonthlyDataPointDto(
    string Month,
    decimal Baseline,
    decimal Projected,
    decimal? BestCase = null,
    decimal? WorstCase = null);

public sealed record ScenarioProjectionDto(
    string CaseType,
    decimal ProjectedValue,
    decimal DeltaValue,
    decimal DeltaPercentage,
    IReadOnlyList<MonthlyDataPointDto>? MonthlyBreakdown = null);

public sealed record ScenarioDto(
    Guid Id,
    Guid WorkspaceId,
    Guid CreatedByUserId,
    string Title,
    string? Description,
    string TargetMetric,
    decimal BaselineValue,
    string TimeHorizon,
    IReadOnlyList<ScenarioVariableDto> Variables,
    IReadOnlyList<ScenarioProjectionDto> Projections,
    string? AiExplanation,
    double ConfidenceScore,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

public sealed record CreateScenarioRequestDto(
    string Title,
    string? Description,
    string TargetMetric,
    decimal BaselineValue,
    string TimeHorizon,
    IReadOnlyList<ScenarioVariableDto> Variables);

public sealed record UpdateScenarioRequestDto(
    string Title,
    string? Description);

public sealed record RerunScenarioRequestDto(
    IReadOnlyList<ScenarioVariableDto>? Variables = null,
    string? TimeHorizon = null);

public sealed record QuickSimulateRequestDto(
    string TargetMetric,
    decimal BaselineValue,
    string TimeHorizon,
    IReadOnlyList<ScenarioVariableDto> Variables);

public sealed record QuickSimulateResponseDto(
    decimal BaselineValue,
    decimal ExpectedValue,
    decimal BestCaseValue,
    decimal WorstCaseValue,
    decimal ExpectedDeltaValue,
    decimal ExpectedDeltaPercentage,
    IReadOnlyList<ScenarioProjectionDto> Projections,
    string? AiExplanation,
    double ConfidenceScore);
