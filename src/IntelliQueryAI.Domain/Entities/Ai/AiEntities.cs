using IntelliQueryAI.Domain.Enums;
using IntelliQueryAI.SharedKernel.Primitives;
using Pgvector;

namespace IntelliQueryAI.Domain.Entities.Ai;

/// <summary>
/// Vector embedding record stored in PostgreSQL PgVector for hybrid semantic retrieval.
/// Enables sub-schema selection without transmitting full database catalogs to LLMs.
/// </summary>
public sealed class EmbeddingRecord : Entity
{
    public Guid WorkspaceId { get; private set; }
    public Guid DataSourceId { get; private set; }
    public EntityType EntityType { get; private set; }
    public string EntityIdentifier { get; private set; } = string.Empty;
    public string ContentText { get; private set; } = string.Empty;
    public Vector Embedding { get; private set; }
    public string? MetadataJson { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private EmbeddingRecord(
        Guid workspaceId,
        Guid dataSourceId,
        EntityType entityType,
        string entityIdentifier,
        string contentText,
        Vector embedding,
        string? metadataJson)
        : base(Guid.NewGuid())
    {
        WorkspaceId = workspaceId;
        DataSourceId = dataSourceId;
        EntityType = entityType;
        EntityIdentifier = entityIdentifier;
        ContentText = contentText;
        Embedding = embedding;
        MetadataJson = metadataJson;
        CreatedAtUtc = DateTime.UtcNow;
    }

    private EmbeddingRecord()
    {
        Embedding = null!;
    }

    public static EmbeddingRecord Create(
        Guid workspaceId,
        Guid dataSourceId,
        EntityType entityType,
        string entityIdentifier,
        string contentText,
        Vector embedding,
        string? metadataJson = null)
    {
        return new EmbeddingRecord(workspaceId, dataSourceId, entityType, entityIdentifier, contentText, embedding, metadataJson);
    }
}

/// <summary>
/// Audit trail of natural language questions, generated SQL, execution metrics, and results.
/// </summary>
public sealed class QueryHistory : Entity
{
    public Guid WorkspaceId { get; private set; }
    public Guid DataSourceId { get; private set; }
    public Guid UserId { get; private set; }
    public Guid? SessionId { get; private set; }
    public string Question { get; private set; } = string.Empty;
    public string GeneratedSql { get; private set; } = string.Empty;
    public long ExecutionTimeMs { get; private set; }
    public int RowCount { get; private set; }
    public bool IsSuccessful { get; private set; }
    public string? ErrorMessage { get; private set; }
    public double ConfidenceScore { get; private set; }
    public DateTime ExecutedAtUtc { get; private set; }

    private QueryHistory(
        Guid workspaceId,
        Guid dataSourceId,
        Guid userId,
        Guid? sessionId,
        string question,
        string generatedSql,
        long executionTimeMs,
        int rowCount,
        bool isSuccessful,
        string? errorMessage,
        double confidenceScore)
        : base(Guid.NewGuid())
    {
        WorkspaceId = workspaceId;
        DataSourceId = dataSourceId;
        UserId = userId;
        SessionId = sessionId;
        Question = question;
        GeneratedSql = generatedSql;
        ExecutionTimeMs = executionTimeMs;
        RowCount = rowCount;
        IsSuccessful = isSuccessful;
        ErrorMessage = errorMessage;
        ConfidenceScore = confidenceScore;
        ExecutedAtUtc = DateTime.UtcNow;
    }

    private QueryHistory() { }

    public static QueryHistory Create(
        Guid workspaceId,
        Guid dataSourceId,
        Guid userId,
        Guid? sessionId,
        string question,
        string generatedSql,
        long executionTimeMs,
        int rowCount,
        bool isSuccessful,
        string? errorMessage = null,
        double confidenceScore = 1.0)
    {
        return new QueryHistory(
            workspaceId,
            dataSourceId,
            userId,
            sessionId,
            question,
            generatedSql,
            executionTimeMs,
            rowCount,
            isSuccessful,
            errorMessage,
            confidenceScore);
    }
}

/// <summary>
/// Versioned prompt template configuring agent roles, instructions, and few-shot examples.
/// </summary>
public sealed class PromptTemplate : Entity
{
    public Guid WorkspaceId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string AgentType { get; private set; } = string.Empty;
    public string SystemPrompt { get; private set; } = string.Empty;
    public string UserPromptTemplate { get; private set; } = string.Empty;
    public int Version { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private PromptTemplate(
        Guid workspaceId,
        string name,
        string agentType,
        string systemPrompt,
        string userPromptTemplate,
        int version)
        : base(Guid.NewGuid())
    {
        WorkspaceId = workspaceId;
        Name = name;
        AgentType = agentType;
        SystemPrompt = systemPrompt;
        UserPromptTemplate = userPromptTemplate;
        Version = version;
        IsActive = true;
        CreatedAtUtc = DateTime.UtcNow;
    }

    private PromptTemplate() { }

    public static PromptTemplate Create(
        Guid workspaceId,
        string name,
        string agentType,
        string systemPrompt,
        string userPromptTemplate,
        int version = 1)
    {
        return new PromptTemplate(workspaceId, name, agentType, systemPrompt, userPromptTemplate, version);
    }
}

/// <summary>
/// Multi-turn interactive conversational session grouping analytical queries.
/// </summary>
public sealed class ConversationSession : AggregateRoot
{
    public Guid WorkspaceId { get; private set; }
    public Guid UserId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string? ContextSummary { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime LastMessageAtUtc { get; private set; }

    private ConversationSession(Guid workspaceId, Guid userId, string title)
        : base(Guid.NewGuid())
    {
        WorkspaceId = workspaceId;
        UserId = userId;
        Title = title;
        CreatedAtUtc = DateTime.UtcNow;
        LastMessageAtUtc = DateTime.UtcNow;
    }

    private ConversationSession() { }

    public static ConversationSession Create(Guid workspaceId, Guid userId, string title)
    {
        return new ConversationSession(workspaceId, userId, title);
    }

    public void UpdateLastMessage(string? summary = null)
    {
        LastMessageAtUtc = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(summary))
        {
            ContextSummary = summary;
        }
    }
}

/// <summary>
/// Saved analytical report containing query, charts, and executive insights.
/// </summary>
public sealed class SavedReport : AggregateRoot
{
    public Guid WorkspaceId { get; private set; }
    public Guid UserId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string Question { get; private set; } = string.Empty;
    public string SqlQuery { get; private set; } = string.Empty;
    public string ChartConfigurationJson { get; private set; } = string.Empty;
    public string InsightsJson { get; private set; } = string.Empty;
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }

    private SavedReport(
        Guid workspaceId,
        Guid userId,
        string title,
        string? description,
        string question,
        string sqlQuery,
        string chartConfigurationJson,
        string insightsJson)
        : base(Guid.NewGuid())
    {
        WorkspaceId = workspaceId;
        UserId = userId;
        Title = title;
        Description = description;
        Question = question;
        SqlQuery = sqlQuery;
        ChartConfigurationJson = chartConfigurationJson;
        InsightsJson = insightsJson;
        CreatedAtUtc = DateTime.UtcNow;
    }

    private SavedReport() { }

    public static SavedReport Create(
        Guid workspaceId,
        Guid userId,
        string title,
        string? description,
        string question,
        string sqlQuery,
        string chartConfigurationJson,
        string insightsJson)
    {
        return new SavedReport(
            workspaceId,
            userId,
            title,
            description,
            question,
            sqlQuery,
            chartConfigurationJson,
            insightsJson);
    }

    public void Update(
        string title,
        string? description,
        string chartConfigurationJson,
        string insightsJson)
    {
        Title = title;
        Description = description;
        ChartConfigurationJson = chartConfigurationJson;
        InsightsJson = insightsJson;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}

/// <summary>
/// High-value statistical observation or anomaly detected from query data.
/// </summary>
public sealed class GeneratedInsight : Entity
{
    public Guid QueryHistoryId { get; private set; }
    public InsightType InsightType { get; private set; }
    public string Summary { get; private set; } = string.Empty;
    public string Detail { get; private set; } = string.Empty;
    public double ImportanceScore { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private GeneratedInsight(
        Guid queryHistoryId,
        InsightType insightType,
        string summary,
        string detail,
        double importanceScore)
        : base(Guid.NewGuid())
    {
        QueryHistoryId = queryHistoryId;
        InsightType = insightType;
        Summary = summary;
        Detail = detail;
        ImportanceScore = importanceScore;
        CreatedAtUtc = DateTime.UtcNow;
    }

    private GeneratedInsight() { }

    public static GeneratedInsight Create(
        Guid queryHistoryId,
        InsightType insightType,
        string summary,
        string detail,
        double importanceScore)
    {
        return new GeneratedInsight(queryHistoryId, insightType, summary, detail, importanceScore);
    }
}
