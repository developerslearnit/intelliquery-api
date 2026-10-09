using IntelliQueryAI.Domain.Entities.Recommendations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IntelliQueryAI.Infrastructure.Persistence.Configurations;

public sealed class RecommendationConfiguration : IEntityTypeConfiguration<Recommendation>
{
    public void Configure(EntityTypeBuilder<Recommendation> builder)
    {
        builder.ToTable("recommendations");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(r => r.WorkspaceId).HasColumnName("workspace_id").IsRequired();
        builder.Property(r => r.CreatedByUserId).HasColumnName("created_by_user_id");

        builder.Property(r => r.Title).HasColumnName("title").HasMaxLength(256).IsRequired();
        builder.Property(r => r.Rationale).HasColumnName("rationale").HasMaxLength(4000).IsRequired();
        builder.Property(r => r.Category).HasColumnName("category").HasMaxLength(128).IsRequired();

        builder.Property(r => r.Priority)
            .HasColumnName("priority")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(r => r.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(r => r.TargetMetric).HasColumnName("target_metric").HasMaxLength(256);
        builder.Property(r => r.ImpactMagnitude).HasColumnName("impact_magnitude").HasMaxLength(256);
        builder.Property(r => r.EstimatedEffort).HasColumnName("estimated_effort").HasMaxLength(128);

        builder.Property(r => r.EvidenceJson).HasColumnName("evidence_json").HasColumnType("text").IsRequired();
        builder.Property(r => r.ActionStepsJson).HasColumnName("action_steps_json").HasColumnType("text").IsRequired();

        builder.Property(r => r.RelatedInvestigationId).HasColumnName("related_investigation_id");
        builder.Property(r => r.RelatedAlertId).HasColumnName("related_alert_id");
        builder.Property(r => r.RelatedMonitorId).HasColumnName("related_monitor_id");
        builder.Property(r => r.RelatedScenarioId).HasColumnName("related_scenario_id");

        builder.Property(r => r.AppliedAtUtc).HasColumnName("applied_at_utc");
        builder.Property(r => r.DismissedAtUtc).HasColumnName("dismissed_at_utc");
        builder.Property(r => r.DismissalReason).HasColumnName("dismissal_reason").HasMaxLength(1000);

        builder.Property(r => r.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.Property(r => r.UpdatedAtUtc).HasColumnName("updated_at_utc");

        builder.HasIndex(r => new { r.WorkspaceId, r.Status })
            .HasDatabaseName("ix_recommendations_workspace_status");

        builder.HasIndex(r => new { r.WorkspaceId, r.CreatedAtUtc })
            .HasDatabaseName("ix_recommendations_workspace_created");

        builder.HasIndex(r => new { r.WorkspaceId, r.Priority })
            .HasDatabaseName("ix_recommendations_workspace_priority");
    }
}
