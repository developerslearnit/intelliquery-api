using IntelliQueryAI.Domain.Entities.Scenarios;

namespace IntelliQueryAI.Domain.Persistence;

public interface IScenarioStore
{
    Task<IReadOnlyList<Scenario>> GetByWorkspaceIdAsync(
        Guid workspaceId,
        int limit = 50,
        CancellationToken cancellationToken = default);

    Task<Scenario?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        Scenario scenario,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(
        Scenario scenario,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        Scenario scenario,
        CancellationToken cancellationToken = default);
}
