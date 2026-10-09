using IntelliQueryAI.Domain.Entities.Recommendations;
using IntelliQueryAI.Domain.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IntelliQueryAI.Infrastructure.Persistence.Stores;

public sealed class EfRecommendationStore : IRecommendationStore
{
    private readonly IntelliQueryDbContext _dbContext;

    public EfRecommendationStore(IntelliQueryDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Recommendation?> GetByIdAsync(Guid id, Guid workspaceId, CancellationToken ct = default)
    {
        return await _dbContext.Recommendations
            .FirstOrDefaultAsync(r => r.Id == id && r.WorkspaceId == workspaceId, ct);
    }

    public async Task<IReadOnlyList<Recommendation>> GetByWorkspaceAsync(
        Guid workspaceId,
        RecommendationStatus? status = null,
        RecommendationPriority? priority = null,
        int limit = 50,
        CancellationToken ct = default)
    {
        var query = _dbContext.Recommendations
            .Where(r => r.WorkspaceId == workspaceId);

        if (status.HasValue)
        {
            query = query.Where(r => r.Status == status.Value);
        }

        if (priority.HasValue)
        {
            query = query.Where(r => r.Priority == priority.Value);
        }

        return await query
            .OrderByDescending(r => r.Priority)
            .ThenByDescending(r => r.CreatedAtUtc)
            .Take(limit)
            .ToListAsync(ct);
    }

    public async Task AddAsync(Recommendation recommendation, CancellationToken ct = default)
    {
        await _dbContext.Recommendations.AddAsync(recommendation, ct);
        await _dbContext.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Recommendation recommendation, CancellationToken ct = default)
    {
        _dbContext.Recommendations.Update(recommendation);
        await _dbContext.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Recommendation recommendation, CancellationToken ct = default)
    {
        _dbContext.Recommendations.Remove(recommendation);
        await _dbContext.SaveChangesAsync(ct);
    }

    public async Task<int> CountByStatusAsync(Guid workspaceId, RecommendationStatus status, CancellationToken ct = default)
    {
        return await _dbContext.Recommendations
            .CountAsync(r => r.WorkspaceId == workspaceId && r.Status == status, ct);
    }

    public async Task<int> CountCriticalActiveAsync(Guid workspaceId, CancellationToken ct = default)
    {
        return await _dbContext.Recommendations
            .CountAsync(r => r.WorkspaceId == workspaceId &&
                             r.Status == RecommendationStatus.Active &&
                             r.Priority == RecommendationPriority.Critical, ct);
    }
}
