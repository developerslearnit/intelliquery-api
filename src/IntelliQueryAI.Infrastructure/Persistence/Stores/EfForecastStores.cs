using IntelliQueryAI.Domain.Entities.Forecasting;
using IntelliQueryAI.Domain.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IntelliQueryAI.Infrastructure.Persistence.Stores;

public sealed class EfForecastStore : IForecastStore
{
    private readonly IntelliQueryDbContext _dbContext;

    public EfForecastStore(IntelliQueryDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Forecast?> GetByIdAsync(Guid id, Guid workspaceId, CancellationToken ct = default)
    {
        return await _dbContext.Forecasts
            .FirstOrDefaultAsync(f => f.Id == id && f.WorkspaceId == workspaceId, ct);
    }

    public async Task<IReadOnlyList<Forecast>> GetByWorkspaceAsync(
        Guid workspaceId,
        string? metricName = null,
        int limit = 50,
        CancellationToken ct = default)
    {
        var query = _dbContext.Forecasts
            .Where(f => f.WorkspaceId == workspaceId);

        if (!string.IsNullOrWhiteSpace(metricName))
        {
            query = query.Where(f => f.MetricName == metricName);
        }

        return await query
            .OrderByDescending(f => f.CreatedAtUtc)
            .Take(limit)
            .ToListAsync(ct);
    }

    public async Task AddAsync(Forecast forecast, CancellationToken ct = default)
    {
        await _dbContext.Forecasts.AddAsync(forecast, ct);
        await _dbContext.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Forecast forecast, CancellationToken ct = default)
    {
        _dbContext.Forecasts.Update(forecast);
        await _dbContext.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Forecast forecast, CancellationToken ct = default)
    {
        _dbContext.Forecasts.Remove(forecast);
        await _dbContext.SaveChangesAsync(ct);
    }

    public async Task<int> CountByWorkspaceAsync(Guid workspaceId, CancellationToken ct = default)
    {
        return await _dbContext.Forecasts
            .CountAsync(f => f.WorkspaceId == workspaceId, ct);
    }
}
