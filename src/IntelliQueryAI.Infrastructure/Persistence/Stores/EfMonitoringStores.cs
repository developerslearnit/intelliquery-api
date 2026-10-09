using IntelliQueryAI.Domain.Entities.Alerts;
using IntelliQueryAI.Domain.Entities.Executive;
using IntelliQueryAI.Domain.Entities.Monitoring;
using IntelliQueryAI.Domain.Enums;
using IntelliQueryAI.Domain.Persistence;
using Microsoft.EntityFrameworkCore;
using Monitor = IntelliQueryAI.Domain.Entities.Monitoring.Monitor;

namespace IntelliQueryAI.Infrastructure.Persistence.Stores;

public sealed class EfMonitorStore : IMonitorStore
{
    private readonly IntelliQueryDbContext _context;

    public EfMonitorStore(IntelliQueryDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(Monitor monitor, CancellationToken cancellationToken = default)
    {
        await _context.Monitors.AddAsync(monitor, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<Monitor?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Monitors
            .Include(m => m.Rules)
            .Include(m => m.Executions.OrderByDescending(e => e.StartedAtUtc).Take(10))
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Monitor>> GetByWorkspaceIdAsync(Guid workspaceId, CancellationToken cancellationToken = default)
    {
        return await _context.Monitors
            .Include(m => m.Rules)
            .Include(m => m.Executions.OrderByDescending(e => e.StartedAtUtc).Take(5))
            .Where(m => m.WorkspaceId == workspaceId)
            .OrderByDescending(m => m.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Monitor>> GetActiveMonitorsAsync(ScheduleType? scheduleType = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Monitors
            .Include(m => m.Rules)
            .Where(m => m.IsActive);

        if (scheduleType.HasValue)
        {
            query = query.Where(m => m.ScheduleType == scheduleType.Value);
        }

        return await query.ToListAsync(cancellationToken);
    }

    public async Task UpdateAsync(Monitor monitor, CancellationToken cancellationToken = default)
    {
        _context.Monitors.Update(monitor);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Monitor monitor, CancellationToken cancellationToken = default)
    {
        _context.Monitors.Remove(monitor);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task AddExecutionAsync(MonitorExecution execution, CancellationToken cancellationToken = default)
    {
        await _context.MonitorExecutions.AddAsync(execution, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MonitorExecution>> GetExecutionsByMonitorIdAsync(Guid monitorId, int limit = 50, CancellationToken cancellationToken = default)
    {
        return await _context.MonitorExecutions
            .Where(e => e.MonitorId == monitorId)
            .OrderByDescending(e => e.StartedAtUtc)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MonitorExecution>> GetRecentExecutionsByWorkspaceIdAsync(Guid workspaceId, int limit = 50, CancellationToken cancellationToken = default)
    {
        return await (from e in _context.MonitorExecutions
                      join m in _context.Monitors on e.MonitorId equals m.Id
                      where m.WorkspaceId == workspaceId
                      orderby e.StartedAtUtc descending
                      select e)
                      .Take(limit)
                      .ToListAsync(cancellationToken);
    }

    public async Task<int> GetCountByWorkspaceIdAsync(Guid workspaceId, CancellationToken cancellationToken = default)
    {
        return await _context.Monitors.CountAsync(m => m.WorkspaceId == workspaceId, cancellationToken);
    }
}

public sealed class EfAlertStore : IAlertStore
{
    private readonly IntelliQueryDbContext _context;

    public EfAlertStore(IntelliQueryDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(Alert alert, CancellationToken cancellationToken = default)
    {
        await _context.Alerts.AddAsync(alert, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<Alert?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Alerts.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Alert>> GetByWorkspaceIdAsync(
        Guid workspaceId,
        bool? onlyOpen = null,
        AlertSeverity? severity = null,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Alerts.Where(a => a.WorkspaceId == workspaceId);

        if (onlyOpen.HasValue && onlyOpen.Value)
        {
            query = query.Where(a => !a.IsAcknowledged);
        }

        if (severity.HasValue)
        {
            query = query.Where(a => a.Severity == severity.Value);
        }

        return await query
            .OrderByDescending(a => a.CreatedAtUtc)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> GetOpenAlertsCountAsync(Guid workspaceId, CancellationToken cancellationToken = default)
    {
        return await _context.Alerts
            .CountAsync(a => a.WorkspaceId == workspaceId && !a.IsAcknowledged, cancellationToken);
    }

    public async Task UpdateAsync(Alert alert, CancellationToken cancellationToken = default)
    {
        _context.Alerts.Update(alert);
        await _context.SaveChangesAsync(cancellationToken);
    }
}

public sealed class EfExecutiveReportStore : IExecutiveReportStore
{
    private readonly IntelliQueryDbContext _context;

    public EfExecutiveReportStore(IntelliQueryDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(ExecutiveReport report, CancellationToken cancellationToken = default)
    {
        await _context.ExecutiveReports.AddAsync(report, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<ExecutiveReport?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.ExecutiveReports.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<ExecutiveReport>> GetByWorkspaceIdAsync(Guid workspaceId, int limit = 20, CancellationToken cancellationToken = default)
    {
        return await _context.ExecutiveReports
            .Where(r => r.WorkspaceId == workspaceId)
            .OrderByDescending(r => r.GeneratedAtUtc)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<ExecutiveReport?> GetLatestByWorkspaceIdAsync(Guid workspaceId, CancellationToken cancellationToken = default)
    {
        return await _context.ExecutiveReports
            .Where(r => r.WorkspaceId == workspaceId)
            .OrderByDescending(r => r.GeneratedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
