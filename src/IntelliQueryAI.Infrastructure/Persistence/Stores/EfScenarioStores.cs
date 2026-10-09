using IntelliQueryAI.Domain.Entities.Scenarios;
using IntelliQueryAI.Domain.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IntelliQueryAI.Infrastructure.Persistence.Stores;

public sealed class EfScenarioStore : IScenarioStore
{
    private readonly IntelliQueryDbContext _dbContext;

    public EfScenarioStore(IntelliQueryDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<Scenario>> GetByWorkspaceIdAsync(
        Guid workspaceId,
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Scenarios
            .AsNoTracking()
            .Where(s => s.WorkspaceId == workspaceId)
            .OrderByDescending(s => s.CreatedAtUtc)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<Scenario?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Scenarios
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
    }

    public async Task AddAsync(
        Scenario scenario,
        CancellationToken cancellationToken = default)
    {
        await _dbContext.Scenarios.AddAsync(scenario, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(
        Scenario scenario,
        CancellationToken cancellationToken = default)
    {
        _dbContext.Scenarios.Update(scenario);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(
        Scenario scenario,
        CancellationToken cancellationToken = default)
    {
        _dbContext.Scenarios.Remove(scenario);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
