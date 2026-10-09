using IntelliQueryAI.Domain.Entities.Ai;
using Pgvector;

namespace IntelliQueryAI.Domain.Persistence;

public sealed record VectorSearchResult(
    Guid Id,
    Guid WorkspaceId,
    Guid DataSourceId,
    string EntityType,
    string EntityIdentifier,
    string ContentText,
    string? MetadataJson,
    double Similarity);

public interface IVectorStore
{
    Task UpsertAsync(EmbeddingRecord record, CancellationToken cancellationToken = default);
    Task UpsertBatchAsync(IReadOnlyList<EmbeddingRecord> records, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<VectorSearchResult>> SearchSimilarAsync(
        Guid workspaceId,
        Guid dataSourceId,
        Vector queryEmbedding,
        int limit = 15,
        double minSimilarity = 0.5,
        CancellationToken cancellationToken = default);
    Task DeleteByDataSourceIdAsync(Guid workspaceId, Guid dataSourceId, CancellationToken cancellationToken = default);
}

public interface IQueryHistoryStore
{
    Task AddAsync(QueryHistory history, CancellationToken cancellationToken = default);
    Task<QueryHistory?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<QueryHistory>> GetByWorkspaceIdAsync(Guid workspaceId, int limit = 50, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<QueryHistory> Items, int TotalCount)> GetPagedByWorkspaceIdAsync(
        Guid workspaceId,
        int page = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default);
}

public interface ISavedReportStore
{
    Task AddAsync(SavedReport report, CancellationToken cancellationToken = default);
    Task<SavedReport?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SavedReport>> GetByWorkspaceIdAsync(Guid workspaceId, CancellationToken cancellationToken = default);
    Task DeleteAsync(SavedReport report, CancellationToken cancellationToken = default);
}

public interface IConversationSessionStore
{
    Task AddAsync(ConversationSession session, CancellationToken cancellationToken = default);
    Task<ConversationSession?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ConversationSession>> GetByUserIdAsync(Guid workspaceId, Guid userId, CancellationToken cancellationToken = default);
}

public interface IPromptTemplateStore
{
    Task<PromptTemplate?> GetActiveTemplateAsync(Guid workspaceId, string agentType, CancellationToken cancellationToken = default);
}
