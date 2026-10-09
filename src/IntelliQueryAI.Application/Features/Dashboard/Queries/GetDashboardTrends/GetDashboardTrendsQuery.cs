using IntelliQueryAI.Application.Abstractions.Messaging;
using IntelliQueryAI.Application.Features.Dashboard.Dtos;
using IntelliQueryAI.Domain.Persistence;
using IntelliQueryAI.Domain.Services;
using IntelliQueryAI.SharedKernel.Results;

namespace IntelliQueryAI.Application.Features.Dashboard.Queries.GetDashboardTrends;

public sealed record GetDashboardTrendsQuery(Guid WorkspaceId, int Days = 30) : IQuery<Result<DashboardTrendsResponseDto>>;

public sealed class GetDashboardTrendsQueryHandler : IQueryHandler<GetDashboardTrendsQuery, Result<DashboardTrendsResponseDto>>
{
    private readonly IQueryHistoryStore _queryHistoryStore;
    private readonly IAnomalyDetectionService _anomalyService;

    public GetDashboardTrendsQueryHandler(
        IQueryHistoryStore queryHistoryStore,
        IAnomalyDetectionService anomalyService)
    {
        _queryHistoryStore = queryHistoryStore;
        _anomalyService = anomalyService;
    }

    public async Task<Result<DashboardTrendsResponseDto>> HandleAsync(
        GetDashboardTrendsQuery query,
        CancellationToken cancellationToken = default)
    {
        var cutoff = DateTime.UtcNow.AddDays(-query.Days);
        var histories = await _queryHistoryStore.GetByWorkspaceIdAsync(query.WorkspaceId, limit: 1000, cancellationToken: cancellationToken);

        var recent = histories.Where(h => h.ExecutedAtUtc >= cutoff).ToList();

        // Group by day for the requested period
        var grouped = recent
            .GroupBy(h => h.ExecutedAtUtc.Date)
            .ToDictionary(g => g.Key, g => g.Count());

        var dataPoints = new List<MetricDataPoint>();
        var trendDtos = new List<TrendPointDto>();

        for (int i = query.Days - 1; i >= 0; i--)
        {
            var date = DateTime.UtcNow.Date.AddDays(-i);
            int count = grouped.GetValueOrDefault(date, 0);

            dataPoints.Add(new MetricDataPoint(date, count, date.ToString("MMM dd")));
            trendDtos.Add(new TrendPointDto(date, date.ToString("MMM dd"), count));
        }

        var trendAnalysis = _anomalyService.AnalyzeTrend(dataPoints);

        decimal avgDaily = dataPoints.Count > 0 ? (decimal)dataPoints.Average(p => (double)p.Value) : 0m;
        decimal totalQueries = recent.Count;

        return Result.Success(new DashboardTrendsResponseDto(
            trendAnalysis.Direction,
            trendAnalysis.GrowthRatePercentage,
            Math.Round(avgDaily, 1),
            totalQueries,
            trendDtos));
    }
}
