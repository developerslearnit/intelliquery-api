using IntelliQueryAI.Domain.Entities.Ai;
using IntelliQueryAI.Domain.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IntelliQueryAI.Infrastructure.Persistence.Stores;

public sealed class EfQueryHistoryStore : IQueryHistoryStore
{
    private readonly IntelliQueryDbContext _context;

    public EfQueryHistoryStore(IntelliQueryDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(QueryHistory history, CancellationToken cancellationToken = default)
    {
        await _context.QueryHistories.AddAsync(history, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<QueryHistory?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.QueryHistories.FirstOrDefaultAsync(q => q.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<QueryHistory>> GetByWorkspaceIdAsync(Guid workspaceId, int limit = 50, CancellationToken cancellationToken = default)
    {
        return await _context.QueryHistories
            .Where(q => q.WorkspaceId == workspaceId)
            .OrderByDescending(q => q.ExecutedAtUtc)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<QueryHistory> Items, int TotalCount)> GetPagedByWorkspaceIdAsync(
        Guid workspaceId,
        int page = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var safePage = page < 1 ? 1 : page;
        var safePageSize = pageSize < 1 ? 10 : (pageSize > 100 ? 100 : pageSize);

        var query = _context.QueryHistories
            .Where(q => q.WorkspaceId == workspaceId);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(q => q.ExecutedAtUtc)
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }
}

public sealed class EfSavedReportStore : ISavedReportStore
{
    private readonly IntelliQueryDbContext _context;

    public EfSavedReportStore(IntelliQueryDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(SavedReport report, CancellationToken cancellationToken = default)
    {
        await _context.SavedReports.AddAsync(report, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<SavedReport?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.SavedReports.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<SavedReport>> GetByWorkspaceIdAsync(Guid workspaceId, CancellationToken cancellationToken = default)
    {
        return await _context.SavedReports
            .Where(r => r.WorkspaceId == workspaceId)
            .OrderByDescending(r => r.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task DeleteAsync(SavedReport report, CancellationToken cancellationToken = default)
    {
        _context.SavedReports.Remove(report);
        await _context.SaveChangesAsync(cancellationToken);
    }
}

public sealed class EfConversationSessionStore : IConversationSessionStore
{
    private readonly IntelliQueryDbContext _context;

    public EfConversationSessionStore(IntelliQueryDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(ConversationSession session, CancellationToken cancellationToken = default)
    {
        await _context.ConversationSessions.AddAsync(session, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<ConversationSession?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.ConversationSessions.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<ConversationSession>> GetByUserIdAsync(Guid workspaceId, Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.ConversationSessions
            .Where(s => s.WorkspaceId == workspaceId && s.UserId == userId)
            .OrderByDescending(s => s.LastMessageAtUtc)
            .ToListAsync(cancellationToken);
    }
}

public sealed class EfPromptTemplateStore : IPromptTemplateStore
{
    private readonly IntelliQueryDbContext _context;

    public EfPromptTemplateStore(IntelliQueryDbContext context)
    {
        _context = context;
    }

    public async Task<PromptTemplate?> GetActiveTemplateAsync(Guid workspaceId, string agentType, CancellationToken cancellationToken = default)
    {
        return await _context.PromptTemplates
            .Where(p => p.WorkspaceId == workspaceId && p.AgentType == agentType && p.IsActive)
            .OrderByDescending(p => p.Version)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
