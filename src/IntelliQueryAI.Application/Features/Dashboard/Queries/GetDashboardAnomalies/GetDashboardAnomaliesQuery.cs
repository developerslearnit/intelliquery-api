using IntelliQueryAI.Application.Abstractions.Messaging;
using IntelliQueryAI.Application.Features.Dashboard.Dtos;
using IntelliQueryAI.Domain.Enums;
using IntelliQueryAI.Domain.Persistence;
using IntelliQueryAI.Domain.Services;
using IntelliQueryAI.SharedKernel.Results;

namespace IntelliQueryAI.Application.Features.Dashboard.Queries.GetDashboardAnomalies;

public sealed record GetDashboardAnomaliesQuery(Guid WorkspaceId) : IQuery<Result<DashboardAnomaliesResponseDto>>;

public sealed class GetDashboardAnomaliesQueryHandler : IQueryHandler<GetDashboardAnomaliesQuery, Result<DashboardAnomaliesResponseDto>>
{
    private readonly IAlertStore _alertStore;
    private readonly IQueryHistoryStore _queryHistoryStore;
    private readonly IAnomalyDetectionService _anomalyService;

    public GetDashboardAnomaliesQueryHandler(
        IAlertStore alertStore,
        IQueryHistoryStore queryHistoryStore,
        IAnomalyDetectionService anomalyService)
    {
        _alertStore = alertStore;
        _queryHistoryStore = queryHistoryStore;
        _anomalyService = anomalyService;
    }

    public async Task<Result<DashboardAnomaliesResponseDto>> HandleAsync(
        GetDashboardAnomaliesQuery query,
        CancellationToken cancellationToken = default)
    {
        var anomalyPoints = new List<AnomalyPointDto>();

        // 1. Fetch existing anomaly alerts from AlertStore
        var alerts = await _alertStore.GetByWorkspaceIdAsync(query.WorkspaceId, onlyOpen: null, limit: 50, cancellationToken: cancellationToken);
        var anomalyAlerts = alerts
            .Where(a => a.AlertType == AlertType.AnomalyDetected || a.AlertType == AlertType.DropSpike)
            .ToList();

        foreach (var a in anomalyAlerts)
        {
            anomalyPoints.Add(new AnomalyPointDto(
                a.CreatedAtUtc,
                a.AlertType.ToString(),
                a.Severity.ToString(),
                a.MetricValue ?? 0m,
                a.ThresholdValue ?? 0m,
                a.ThresholdValue.HasValue && a.ThresholdValue.Value != 0
                    ? Math.Round(((a.MetricValue ?? 0m) - a.ThresholdValue.Value) / Math.Abs(a.ThresholdValue.Value) * 100m, 1)
                    : 0m,
                2.8,
                a.Description));
        }

        // 2. Also run statistical analysis over recent query latency data
        var histories = await _queryHistoryStore.GetByWorkspaceIdAsync(query.WorkspaceId, limit: 100, cancellationToken: cancellationToken);
        if (histories.Count >= 5)
        {
            var latencyPoints = histories
                .OrderBy(h => h.ExecutedAtUtc)
                .Select(h => new MetricDataPoint(h.ExecutedAtUtc, (decimal)h.ExecutionTimeMs, "QueryLatencyMs"))
                .ToList();

            var detected = _anomalyService.AnalyzeSeries(latencyPoints, zScoreThreshold: 2.5, percentageThreshold: 50.0m);
            foreach (var d in detected)
            {
                anomalyPoints.Add(new AnomalyPointDto(
                    d.DetectedAtUtc,
                    "QueryLatencySpike",
                    d.Severity.ToString(),
                    d.CurrentValue,
                    d.ExpectedValue,
                    d.DeviationPercentage,
                    d.ZScore,
                    d.Description));
            }
        }

        return Result.Success(new DashboardAnomaliesResponseDto(
            anomalyPoints.Count,
            anomalyPoints.OrderByDescending(p => p.DetectedAtUtc).Take(20).ToList()));
    }
}
