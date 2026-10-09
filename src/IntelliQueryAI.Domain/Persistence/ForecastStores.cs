using IntelliQueryAI.Domain.Entities.Forecasting;

namespace IntelliQueryAI.Domain.Persistence;

public interface IForecastStore
{
    Task<Forecast?> GetByIdAsync(Guid id, Guid workspaceId, CancellationToken ct = default);
    Task<IReadOnlyList<Forecast>> GetByWorkspaceAsync(
        Guid workspaceId,
        string? metricName = null,
        int limit = 50,
        CancellationToken ct = default);
    Task AddAsync(Forecast forecast, CancellationToken ct = default);
    Task UpdateAsync(Forecast forecast, CancellationToken ct = default);
    Task DeleteAsync(Forecast forecast, CancellationToken ct = default);
    Task<int> CountByWorkspaceAsync(Guid workspaceId, CancellationToken ct = default);
}
