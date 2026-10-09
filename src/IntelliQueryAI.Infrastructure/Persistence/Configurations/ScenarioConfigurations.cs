using IntelliQueryAI.Domain.Entities.Scenarios;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IntelliQueryAI.Infrastructure.Persistence.Configurations;

public sealed class ScenarioConfiguration : IEntityTypeConfiguration<Scenario>
{
    public void Configure(EntityTypeBuilder<Scenario> builder)
    {
        builder.ToTable("scenarios");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("id");

        builder.Property(s => s.WorkspaceId)
            .HasColumnName("workspace_id")
            .IsRequired();

        builder.Property(s => s.CreatedByUserId)
            .HasColumnName("created_by_user_id")
            .IsRequired();

        builder.Property(s => s.Title)
            .HasColumnName("title")
            .HasMaxLength(250)
            .IsRequired();

        builder.Property(s => s.Description)
            .HasColumnName("description")
            .HasMaxLength(1000);

        builder.Property(s => s.TargetMetric)
            .HasColumnName("target_metric")
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(s => s.BaselineValue)
            .HasColumnName("baseline_value")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(s => s.TimeHorizon)
            .HasColumnName("time_horizon")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(s => s.VariablesJson)
            .HasColumnName("variables_json")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(s => s.ProjectionsJson)
            .HasColumnName("projections_json")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(s => s.AiExplanation)
            .HasColumnName("ai_explanation");

        builder.Property(s => s.ConfidenceScore)
            .HasColumnName("confidence_score")
            .IsRequired();

        builder.Property(s => s.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.Property(s => s.UpdatedAtUtc)
            .HasColumnName("updated_at_utc");

        builder.HasIndex(s => new { s.WorkspaceId, s.CreatedAtUtc })
            .HasDatabaseName("ix_scenarios_workspace_created");

        builder.HasIndex(s => new { s.WorkspaceId, s.TargetMetric })
            .HasDatabaseName("ix_scenarios_metric");
    }
}
