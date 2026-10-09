using IntelliQueryAI.Domain.Entities.Governance;

namespace IntelliQueryAI.Domain.Persistence;

public interface IGovernancePolicyStore
{
    Task<IReadOnlyList<GovernancePolicy>> GetByWorkspaceIdAsync(
        Guid workspaceId,
        bool activeOnly = false,
        CancellationToken cancellationToken = default);

    Task<GovernancePolicy?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GovernancePolicy>> GetMatchingPoliciesAsync(
        Guid workspaceId,
        string? tableName,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        GovernancePolicy policy,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(
        GovernancePolicy policy,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        GovernancePolicy policy,
        CancellationToken cancellationToken = default);
}
