using IntelliQueryAI.Domain.Entities.Investigations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IntelliQueryAI.Infrastructure.Persistence.Configurations;

public sealed class InvestigationReportConfiguration : IEntityTypeConfiguration<InvestigationReport>
{
    public void Configure(EntityTypeBuilder<InvestigationReport> builder)
    {
        builder.ToTable("investigation_reports");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.WorkspaceId)
            .IsRequired();

        builder.Property(r => r.DataSourceId);

        builder.Property(r => r.Question)
            .HasMaxLength(2000)
            .IsRequired();

        builder.Property(r => r.TargetMetric)
            .HasMaxLength(200);

        builder.Property(r => r.Summary)
            .IsRequired();

        builder.Property(r => r.ConfidenceScore)
            .IsRequired();

        builder.Property(r => r.Status)
            .HasMaxLength(50)
            .HasDefaultValue("Completed")
            .IsRequired();

        builder.Property(r => r.RootCausesJson)
            .HasColumnType("jsonb")
            .HasDefaultValue("[]")
            .IsRequired();

        builder.Property(r => r.ContributorsJson)
            .HasColumnType("jsonb")
            .HasDefaultValue("[]")
            .IsRequired();

        builder.Property(r => r.EvidenceJson)
            .HasColumnType("jsonb")
            .HasDefaultValue("[]")
            .IsRequired();

        builder.Property(r => r.RecommendationsJson)
            .HasColumnType("jsonb")
            .HasDefaultValue("[]")
            .IsRequired();

        builder.Property(r => r.QueriesExecutedJson)
            .HasColumnType("jsonb")
            .HasDefaultValue("[]")
            .IsRequired();

        builder.Property(r => r.FollowUpQuestionsJson)
            .HasColumnType("jsonb")
            .HasDefaultValue("[]")
            .IsRequired();

        builder.Property(r => r.DurationMs)
            .IsRequired();

        builder.Property(r => r.TokensUsed)
            .IsRequired();

        builder.Property(r => r.CreatedAtUtc)
            .IsRequired();

        builder.HasIndex(r => new { r.WorkspaceId, r.CreatedAtUtc })
            .HasDatabaseName("ix_investigation_reports_workspace_created");
    }
}
