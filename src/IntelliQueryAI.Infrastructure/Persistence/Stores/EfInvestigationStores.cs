using IntelliQueryAI.Domain.Entities.Investigations;
using IntelliQueryAI.Domain.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IntelliQueryAI.Infrastructure.Persistence.Stores;

public sealed class EfInvestigationReportStore : IInvestigationReportStore
{
    private readonly IntelliQueryDbContext _context;

    public EfInvestigationReportStore(IntelliQueryDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(InvestigationReport report, CancellationToken cancellationToken = default)
    {
        await _context.InvestigationReports.AddAsync(report, cancellationToken);
    }

    public async Task<InvestigationReport?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.InvestigationReports
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<InvestigationReport>> GetByWorkspaceIdAsync(
        Guid workspaceId,
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        return await _context.InvestigationReports
            .Where(r => r.WorkspaceId == workspaceId)
            .OrderByDescending(r => r.CreatedAtUtc)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public Task DeleteAsync(InvestigationReport report, CancellationToken cancellationToken = default)
    {
        _context.InvestigationReports.Remove(report);
        return Task.CompletedTask;
    }
}
