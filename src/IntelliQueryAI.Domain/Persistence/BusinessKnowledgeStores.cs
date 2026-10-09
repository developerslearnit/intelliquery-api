using IntelliQueryAI.Domain.Entities.BusinessKnowledge;

namespace IntelliQueryAI.Domain.Persistence;

public interface IBusinessTermStore
{
    Task AddAsync(BusinessTerm term, CancellationToken cancellationToken = default);
    Task<BusinessTerm?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BusinessTerm>> GetByWorkspaceIdAsync(
        Guid workspaceId,
        string? search = null,
        string? category = null,
        bool? isActive = null,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BusinessTerm>> SearchRelevantTermsAsync(
        Guid workspaceId,
        IEnumerable<string> keywords,
        CancellationToken cancellationToken = default);
    Task UpdateAsync(BusinessTerm term, CancellationToken cancellationToken = default);
    Task DeleteAsync(BusinessTerm term, CancellationToken cancellationToken = default);
}

public interface IMetricStore
{
    Task AddAsync(MetricDefinition metric, CancellationToken cancellationToken = default);
    Task<MetricDefinition?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MetricDefinition>> GetByWorkspaceIdAsync(
        Guid workspaceId,
        string? search = null,
        bool? isActive = null,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MetricDefinition>> SearchRelevantMetricsAsync(
        Guid workspaceId,
        IEnumerable<string> keywords,
        CancellationToken cancellationToken = default);
    Task UpdateAsync(MetricDefinition metric, CancellationToken cancellationToken = default);
    Task DeleteAsync(MetricDefinition metric, CancellationToken cancellationToken = default);
}
