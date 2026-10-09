using IntelliQueryAI.Domain.Entities.Governance;
using IntelliQueryAI.Domain.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IntelliQueryAI.Infrastructure.Persistence.Stores;

public sealed class EfGovernancePolicyStore : IGovernancePolicyStore
{
    private readonly IntelliQueryDbContext _context;

    public EfGovernancePolicyStore(IntelliQueryDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(GovernancePolicy policy, CancellationToken cancellationToken = default)
    {
        await _context.GovernancePolicies.AddAsync(policy, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<GovernancePolicy?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.GovernancePolicies
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<GovernancePolicy>> GetByWorkspaceIdAsync(
        Guid workspaceId,
        bool activeOnly = false,
        CancellationToken cancellationToken = default)
    {
        var query = _context.GovernancePolicies
            .Where(p => p.WorkspaceId == workspaceId);

        if (activeOnly)
        {
            query = query.Where(p => p.IsActive);
        }

        return await query
            .OrderByDescending(p => p.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<GovernancePolicy>> GetMatchingPoliciesAsync(
        Guid workspaceId,
        string? tableName,
        CancellationToken cancellationToken = default)
    {
        var query = _context.GovernancePolicies
            .Where(p => p.WorkspaceId == workspaceId && p.IsActive);

        if (!string.IsNullOrWhiteSpace(tableName))
        {
            query = query.Where(p => p.TargetTable == "*" || p.TargetTable.ToLower() == tableName.ToLower());
        }

        return await query.ToListAsync(cancellationToken);
    }

    public async Task UpdateAsync(GovernancePolicy policy, CancellationToken cancellationToken = default)
    {
        _context.GovernancePolicies.Update(policy);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(GovernancePolicy policy, CancellationToken cancellationToken = default)
    {
        _context.GovernancePolicies.Remove(policy);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
