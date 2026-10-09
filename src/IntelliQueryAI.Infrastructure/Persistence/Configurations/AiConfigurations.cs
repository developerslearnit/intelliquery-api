using IntelliQueryAI.Domain.Entities.Ai;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IntelliQueryAI.Infrastructure.Persistence.Configurations;

public sealed class EmbeddingRecordConfiguration : IEntityTypeConfiguration<EmbeddingRecord>
{
    public void Configure(EntityTypeBuilder<EmbeddingRecord> builder)
    {
        builder.ToTable("embedding_records");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.WorkspaceId)
            .IsRequired();

        builder.Property(e => e.DataSourceId)
            .IsRequired();

        builder.Property(e => e.EntityType)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.EntityIdentifier)
            .HasMaxLength(250)
            .IsRequired();

        builder.Property(e => e.ContentText)
            .IsRequired();

        builder.Property(e => e.Embedding)
            .HasColumnType("vector(1536)")
            .IsRequired();

        builder.Property(e => e.MetadataJson)
            .HasColumnType("jsonb");

        builder.Property(e => e.CreatedAtUtc)
            .IsRequired();

        // Optimized composite indexes for tenant filtering
        builder.HasIndex(e => new { e.WorkspaceId, e.DataSourceId })
            .HasDatabaseName("ix_embedding_records_workspace_datasource");

        builder.HasIndex(e => new { e.WorkspaceId, e.EntityType })
            .HasDatabaseName("ix_embedding_records_workspace_type");

        // HNSW Cosine vector index for high-speed nearest-neighbor retrieval
        builder.HasIndex(e => e.Embedding)
            .HasMethod("hnsw")
            .HasOperators("vector_cosine_ops")
            .HasDatabaseName("ix_embedding_records_vector_hnsw");
    }
}

public sealed class QueryHistoryConfiguration : IEntityTypeConfiguration<QueryHistory>
{
    public void Configure(EntityTypeBuilder<QueryHistory> builder)
    {
        builder.ToTable("query_histories");

        builder.HasKey(q => q.Id);

        builder.Property(q => q.WorkspaceId).IsRequired();
        builder.Property(q => q.DataSourceId).IsRequired();
        builder.Property(q => q.UserId).IsRequired();
        builder.Property(q => q.Question).IsRequired();
        builder.Property(q => q.GeneratedSql).IsRequired();
        builder.Property(q => q.ExecutionTimeMs).IsRequired();
        builder.Property(q => q.RowCount).IsRequired();
        builder.Property(q => q.IsSuccessful).IsRequired();
        builder.Property(q => q.ConfidenceScore).IsRequired();
        builder.Property(q => q.ExecutedAtUtc).IsRequired();

        builder.HasIndex(q => new { q.WorkspaceId, q.ExecutedAtUtc })
            .HasDatabaseName("ix_query_histories_workspace_executed");

        builder.HasIndex(q => new { q.WorkspaceId, q.DataSourceId })
            .HasDatabaseName("ix_query_histories_workspace_datasource");
    }
}

public sealed class PromptTemplateConfiguration : IEntityTypeConfiguration<PromptTemplate>
{
    public void Configure(EntityTypeBuilder<PromptTemplate> builder)
    {
        builder.ToTable("prompt_templates");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.WorkspaceId).IsRequired();
        builder.Property(p => p.Name).HasMaxLength(100).IsRequired();
        builder.Property(p => p.AgentType).HasMaxLength(100).IsRequired();
        builder.Property(p => p.SystemPrompt).IsRequired();
        builder.Property(p => p.UserPromptTemplate).IsRequired();
        builder.Property(p => p.Version).IsRequired();
        builder.Property(p => p.IsActive).IsRequired();
        builder.Property(p => p.CreatedAtUtc).IsRequired();

        builder.HasIndex(p => new { p.WorkspaceId, p.AgentType, p.Version })
            .IsUnique()
            .HasDatabaseName("ix_prompt_templates_workspace_agent_version");
    }
}

public sealed class ConversationSessionConfiguration : IEntityTypeConfiguration<ConversationSession>
{
    public void Configure(EntityTypeBuilder<ConversationSession> builder)
    {
        builder.ToTable("conversation_sessions");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.WorkspaceId).IsRequired();
        builder.Property(s => s.UserId).IsRequired();
        builder.Property(s => s.Title).HasMaxLength(200).IsRequired();
        builder.Property(s => s.ContextSummary).HasMaxLength(2000);
        builder.Property(s => s.CreatedAtUtc).IsRequired();
        builder.Property(s => s.LastMessageAtUtc).IsRequired();

        builder.HasIndex(s => new { s.WorkspaceId, s.UserId })
            .HasDatabaseName("ix_conversation_sessions_workspace_user");
    }
}

public sealed class SavedReportConfiguration : IEntityTypeConfiguration<SavedReport>
{
    public void Configure(EntityTypeBuilder<SavedReport> builder)
    {
        builder.ToTable("saved_reports");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.WorkspaceId).IsRequired();
        builder.Property(r => r.UserId).IsRequired();
        builder.Property(r => r.Title).HasMaxLength(200).IsRequired();
        builder.Property(r => r.Description).HasMaxLength(1000);
        builder.Property(r => r.Question).IsRequired();
        builder.Property(r => r.SqlQuery).IsRequired();
        builder.Property(r => r.ChartConfigurationJson).HasColumnType("jsonb").IsRequired();
        builder.Property(r => r.InsightsJson).HasColumnType("jsonb").IsRequired();
        builder.Property(r => r.CreatedAtUtc).IsRequired();

        builder.HasIndex(r => new { r.WorkspaceId, r.UserId })
            .HasDatabaseName("ix_saved_reports_workspace_user");
    }
}

public sealed class GeneratedInsightConfiguration : IEntityTypeConfiguration<GeneratedInsight>
{
    public void Configure(EntityTypeBuilder<GeneratedInsight> builder)
    {
        builder.ToTable("generated_insights");

        builder.HasKey(g => g.Id);

        builder.Property(g => g.QueryHistoryId).IsRequired();
        builder.Property(g => g.InsightType).HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(g => g.Summary).HasMaxLength(500).IsRequired();
        builder.Property(g => g.Detail).IsRequired();
        builder.Property(g => g.ImportanceScore).IsRequired();
        builder.Property(g => g.CreatedAtUtc).IsRequired();

        builder.HasIndex(g => g.QueryHistoryId)
            .HasDatabaseName("ix_generated_insights_query_history");
    }
}
