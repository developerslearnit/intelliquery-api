using IntelliQueryAI.Domain.Entities.Forecasting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IntelliQueryAI.Infrastructure.Persistence.Configurations;

public sealed class ForecastConfiguration : IEntityTypeConfiguration<Forecast>
{
    public void Configure(EntityTypeBuilder<Forecast> builder)
    {
        builder.ToTable("forecasts");

        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(f => f.WorkspaceId).HasColumnName("workspace_id").IsRequired();
        builder.Property(f => f.CreatedByUserId).HasColumnName("created_by_user_id");
        builder.Property(f => f.DataSourceId).HasColumnName("data_source_id");

        builder.Property(f => f.MetricName).HasColumnName("metric_name").HasMaxLength(256).IsRequired();
        builder.Property(f => f.ForecastPeriod).HasColumnName("forecast_period").HasMaxLength(128).IsRequired();
        builder.Property(f => f.HistoricalPeriod).HasColumnName("historical_period").HasMaxLength(128).IsRequired();

        builder.Property(f => f.Method)
            .HasColumnName("method")
            .HasConversion<string>()
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(f => f.ConfidenceInterval).HasColumnName("confidence_interval").IsRequired();

        builder.Property(f => f.HistoricalDataPointsJson).HasColumnName("historical_data_points_json").HasColumnType("text").IsRequired();
        builder.Property(f => f.ForecastDataPointsJson).HasColumnName("forecast_data_points_json").HasColumnType("text").IsRequired();
        builder.Property(f => f.AssumptionsJson).HasColumnName("assumptions_json").HasColumnType("text").IsRequired();
        builder.Property(f => f.AiExplanation).HasColumnName("ai_explanation").HasColumnType("text");

        builder.Property(f => f.ProjectedGrowthRate).HasColumnName("projected_growth_rate").HasPrecision(18, 4);
        builder.Property(f => f.FinalProjectedValue).HasColumnName("final_projected_value").HasPrecision(18, 4);

        builder.Property(f => f.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.Property(f => f.UpdatedAtUtc).HasColumnName("updated_at_utc");

        builder.HasIndex(f => new { f.WorkspaceId, f.MetricName })
            .HasDatabaseName("ix_forecasts_workspace_metric");

        builder.HasIndex(f => new { f.WorkspaceId, f.CreatedAtUtc })
            .HasDatabaseName("ix_forecasts_workspace_created");
    }
}
