using IntelliQueryAI.Domain.Entities.Dashboards;
using IntelliQueryAI.Domain.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IntelliQueryAI.Infrastructure.Persistence.Stores;

public sealed class EfCustomDashboardStore : ICustomDashboardStore
{
    private readonly IntelliQueryDbContext _context;

    public EfCustomDashboardStore(IntelliQueryDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(CustomDashboard dashboard, CancellationToken cancellationToken = default)
    {
        await _context.CustomDashboards.AddAsync(dashboard, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<CustomDashboard?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.CustomDashboards
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<CustomDashboard>> GetByWorkspaceIdAsync(Guid workspaceId, CancellationToken cancellationToken = default)
    {
        return await _context.CustomDashboards
            .Where(d => d.WorkspaceId == workspaceId)
            .OrderByDescending(d => d.IsDefault)
            .ThenByDescending(d => d.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task UpdateAsync(CustomDashboard dashboard, CancellationToken cancellationToken = default)
    {
        _context.CustomDashboards.Update(dashboard);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(CustomDashboard dashboard, CancellationToken cancellationToken = default)
    {
        _context.CustomDashboards.Remove(dashboard);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
