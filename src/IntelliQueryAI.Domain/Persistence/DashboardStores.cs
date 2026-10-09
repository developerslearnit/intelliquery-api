using IntelliQueryAI.Domain.Entities.Dashboards;

namespace IntelliQueryAI.Domain.Persistence;

public interface ICustomDashboardStore
{
    Task AddAsync(CustomDashboard dashboard, CancellationToken cancellationToken = default);
    Task<CustomDashboard?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CustomDashboard>> GetByWorkspaceIdAsync(Guid workspaceId, CancellationToken cancellationToken = default);
    Task UpdateAsync(CustomDashboard dashboard, CancellationToken cancellationToken = default);
    Task DeleteAsync(CustomDashboard dashboard, CancellationToken cancellationToken = default);
}
