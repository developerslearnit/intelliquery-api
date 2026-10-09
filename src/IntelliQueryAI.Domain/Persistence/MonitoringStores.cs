using IntelliQueryAI.Domain.Entities.Alerts;
using IntelliQueryAI.Domain.Entities.Executive;
using IntelliQueryAI.Domain.Entities.Monitoring;
using IntelliQueryAI.Domain.Enums;
using Monitor = IntelliQueryAI.Domain.Entities.Monitoring.Monitor;

namespace IntelliQueryAI.Domain.Persistence;

public interface IMonitorStore
{
    Task AddAsync(Monitor monitor, CancellationToken cancellationToken = default);
    Task<Monitor?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Monitor>> GetByWorkspaceIdAsync(Guid workspaceId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Monitor>> GetActiveMonitorsAsync(ScheduleType? scheduleType = null, CancellationToken cancellationToken = default);
    Task UpdateAsync(Monitor monitor, CancellationToken cancellationToken = default);
    Task DeleteAsync(Monitor monitor, CancellationToken cancellationToken = default);

    Task AddExecutionAsync(MonitorExecution execution, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MonitorExecution>> GetExecutionsByMonitorIdAsync(Guid monitorId, int limit = 50, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MonitorExecution>> GetRecentExecutionsByWorkspaceIdAsync(Guid workspaceId, int limit = 50, CancellationToken cancellationToken = default);
    Task<int> GetCountByWorkspaceIdAsync(Guid workspaceId, CancellationToken cancellationToken = default);
}

public interface IAlertStore
{
    Task AddAsync(Alert alert, CancellationToken cancellationToken = default);
    Task<Alert?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Alert>> GetByWorkspaceIdAsync(
        Guid workspaceId,
        bool? onlyOpen = null,
        AlertSeverity? severity = null,
        int limit = 100,
        CancellationToken cancellationToken = default);
    Task<int> GetOpenAlertsCountAsync(Guid workspaceId, CancellationToken cancellationToken = default);
    Task UpdateAsync(Alert alert, CancellationToken cancellationToken = default);
}

public interface IExecutiveReportStore
{
    Task AddAsync(ExecutiveReport report, CancellationToken cancellationToken = default);
    Task<ExecutiveReport?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ExecutiveReport>> GetByWorkspaceIdAsync(Guid workspaceId, int limit = 20, CancellationToken cancellationToken = default);
    Task<ExecutiveReport?> GetLatestByWorkspaceIdAsync(Guid workspaceId, CancellationToken cancellationToken = default);
}
