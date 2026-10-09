using IntelliQueryAI.Domain.Entities.Investigations;

namespace IntelliQueryAI.Domain.Persistence;

public interface IInvestigationReportStore
{
    Task AddAsync(InvestigationReport report, CancellationToken cancellationToken = default);
    Task<InvestigationReport?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InvestigationReport>> GetByWorkspaceIdAsync(
        Guid workspaceId,
        int limit = 50,
        CancellationToken cancellationToken = default);
    Task DeleteAsync(InvestigationReport report, CancellationToken cancellationToken = default);
}
