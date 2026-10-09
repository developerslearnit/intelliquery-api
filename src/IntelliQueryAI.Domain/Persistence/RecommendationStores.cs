using IntelliQueryAI.Domain.Entities.Recommendations;

namespace IntelliQueryAI.Domain.Persistence;

public interface IRecommendationStore
{
    Task<Recommendation?> GetByIdAsync(Guid id, Guid workspaceId, CancellationToken ct = default);
    Task<IReadOnlyList<Recommendation>> GetByWorkspaceAsync(
        Guid workspaceId,
        RecommendationStatus? status = null,
        RecommendationPriority? priority = null,
        int limit = 50,
        CancellationToken ct = default);
    Task AddAsync(Recommendation recommendation, CancellationToken ct = default);
    Task UpdateAsync(Recommendation recommendation, CancellationToken ct = default);
    Task DeleteAsync(Recommendation recommendation, CancellationToken ct = default);
    Task<int> CountByStatusAsync(Guid workspaceId, RecommendationStatus status, CancellationToken ct = default);
    Task<int> CountCriticalActiveAsync(Guid workspaceId, CancellationToken ct = default);
}
