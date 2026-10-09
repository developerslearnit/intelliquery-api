using IntelliQueryAI.Domain.Entities.BusinessKnowledge;
using IntelliQueryAI.Domain.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IntelliQueryAI.Infrastructure.Persistence.Stores;

public sealed class EfBusinessTermStore : IBusinessTermStore
{
    private readonly IntelliQueryDbContext _context;

    public EfBusinessTermStore(IntelliQueryDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(BusinessTerm term, CancellationToken cancellationToken = default)
    {
        await _context.BusinessTerms.AddAsync(term, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<BusinessTerm?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.BusinessTerms.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<BusinessTerm>> GetByWorkspaceIdAsync(
        Guid workspaceId,
        string? search = null,
        string? category = null,
        bool? isActive = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.BusinessTerms.Where(t => t.WorkspaceId == workspaceId);

        if (isActive.HasValue)
        {
            query = query.Where(t => t.IsActive == isActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(category) && !category.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(t => t.Category == category);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            string lowerSearch = search.Trim().ToLowerInvariant();
            query = query.Where(t =>
                t.Name.ToLower().Contains(lowerSearch) ||
                t.Definition.ToLower().Contains(lowerSearch) ||
                t.AliasesJson.ToLower().Contains(lowerSearch) ||
                (t.TargetTable != null && t.TargetTable.ToLower().Contains(lowerSearch)));
        }

        return await query
            .OrderBy(t => t.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<BusinessTerm>> SearchRelevantTermsAsync(
        Guid workspaceId,
        IEnumerable<string> keywords,
        CancellationToken cancellationToken = default)
    {
        var cleanKeywords = keywords
            .Where(k => !string.IsNullOrWhiteSpace(k) && k.Length > 2)
            .Select(k => k.Trim().ToLowerInvariant())
            .Distinct()
            .Take(15)
            .ToList();

        if (cleanKeywords.Count == 0)
        {
            return Array.Empty<BusinessTerm>();
        }

        var allActive = await _context.BusinessTerms
            .Where(t => t.WorkspaceId == workspaceId && t.IsActive)
            .ToListAsync(cancellationToken);

        // In-memory scoring for flexible alias and substring matching
        var matched = allActive
            .Where(term =>
            {
                string nameLower = term.Name.ToLowerInvariant();
                string aliasesLower = term.AliasesJson.ToLowerInvariant();
                string defLower = term.Definition.ToLowerInvariant();

                return cleanKeywords.Any(k =>
                    nameLower.Contains(k) ||
                    aliasesLower.Contains(k) ||
                    defLower.Contains(k));
            })
            .Take(10)
            .ToList();

        return matched;
    }

    public async Task UpdateAsync(BusinessTerm term, CancellationToken cancellationToken = default)
    {
        _context.BusinessTerms.Update(term);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(BusinessTerm term, CancellationToken cancellationToken = default)
    {
        _context.BusinessTerms.Remove(term);
        await _context.SaveChangesAsync(cancellationToken);
    }
}

public sealed class EfMetricStore : IMetricStore
{
    private readonly IntelliQueryDbContext _context;

    public EfMetricStore(IntelliQueryDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(MetricDefinition metric, CancellationToken cancellationToken = default)
    {
        await _context.MetricDefinitions.AddAsync(metric, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<MetricDefinition?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.MetricDefinitions.FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<MetricDefinition>> GetByWorkspaceIdAsync(
        Guid workspaceId,
        string? search = null,
        bool? isActive = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.MetricDefinitions.Where(m => m.WorkspaceId == workspaceId);

        if (isActive.HasValue)
        {
            query = query.Where(m => m.IsActive == isActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            string lowerSearch = search.Trim().ToLowerInvariant();
            query = query.Where(m =>
                m.Name.ToLower().Contains(lowerSearch) ||
                m.Description.ToLower().Contains(lowerSearch) ||
                (m.TargetTable != null && m.TargetTable.ToLower().Contains(lowerSearch)) ||
                (m.CalculationFormula != null && m.CalculationFormula.ToLower().Contains(lowerSearch)));
        }

        return await query
            .OrderBy(m => m.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MetricDefinition>> SearchRelevantMetricsAsync(
        Guid workspaceId,
        IEnumerable<string> keywords,
        CancellationToken cancellationToken = default)
    {
        var cleanKeywords = keywords
            .Where(k => !string.IsNullOrWhiteSpace(k) && k.Length > 2)
            .Select(k => k.Trim().ToLowerInvariant())
            .Distinct()
            .Take(15)
            .ToList();

        if (cleanKeywords.Count == 0)
        {
            return Array.Empty<MetricDefinition>();
        }

        var allActive = await _context.MetricDefinitions
            .Where(m => m.WorkspaceId == workspaceId && m.IsActive)
            .ToListAsync(cancellationToken);

        var matched = allActive
            .Where(m =>
            {
                string nameLower = m.Name.ToLowerInvariant();
                string descLower = m.Description.ToLowerInvariant();
                string tableLower = (m.TargetTable ?? string.Empty).ToLowerInvariant();

                return cleanKeywords.Any(k =>
                    nameLower.Contains(k) ||
                    descLower.Contains(k) ||
                    tableLower.Contains(k));
            })
            .Take(10)
            .ToList();

        return matched;
    }

    public async Task UpdateAsync(MetricDefinition metric, CancellationToken cancellationToken = default)
    {
        _context.MetricDefinitions.Update(metric);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(MetricDefinition metric, CancellationToken cancellationToken = default)
    {
        _context.MetricDefinitions.Remove(metric);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
