using IntelliQueryAI.Domain.Entities.Alerts;
using IntelliQueryAI.Domain.Entities.Executive;
using IntelliQueryAI.Domain.Entities.Monitoring;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Monitor = IntelliQueryAI.Domain.Entities.Monitoring.Monitor;

namespace IntelliQueryAI.Infrastructure.Persistence.Configurations;

public sealed class MonitorConfiguration : IEntityTypeConfiguration<Monitor>
{
    public void Configure(EntityTypeBuilder<Monitor> builder)
    {
        builder.ToTable("monitors");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.WorkspaceId)
            .IsRequired();

        builder.Property(m => m.DataSourceId)
            .IsRequired();

        builder.Property(m => m.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(m => m.Description)
            .HasMaxLength(1000);

        builder.Property(m => m.MonitorType)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(m => m.IsActive)
            .IsRequired();

        builder.Property(m => m.ScheduleType)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(m => m.QueryTemplate)
            .HasMaxLength(4000);

        builder.Property(m => m.TargetMetric)
            .HasMaxLength(200);

        builder.Property(m => m.CreatedAtUtc)
            .IsRequired();

        builder.HasMany(m => m.Rules)
            .WithOne()
            .HasForeignKey(r => r.MonitorId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(m => m.Executions)
            .WithOne()
            .HasForeignKey(e => e.MonitorId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(m => new { m.WorkspaceId, m.IsActive })
            .HasDatabaseName("ix_monitors_workspace_active");

        builder.HasIndex(m => new { m.ScheduleType, m.IsActive })
            .HasDatabaseName("ix_monitors_schedule_active");
    }
}

public sealed class MonitorRuleConfiguration : IEntityTypeConfiguration<MonitorRule>
{
    public void Configure(EntityTypeBuilder<MonitorRule> builder)
    {
        builder.ToTable("monitor_rules");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.MonitorId)
            .IsRequired();

        builder.Property(r => r.RuleType)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(r => r.ThresholdValue)
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(r => r.ComparisonOperator)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(r => r.TimeWindow)
            .HasMaxLength(50);

        builder.Property(r => r.CreatedAtUtc)
            .IsRequired();

        builder.HasIndex(r => r.MonitorId)
            .HasDatabaseName("ix_monitor_rules_monitor_id");
    }
}

public sealed class MonitorExecutionConfiguration : IEntityTypeConfiguration<MonitorExecution>
{
    public void Configure(EntityTypeBuilder<MonitorExecution> builder)
    {
        builder.ToTable("monitor_executions");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.MonitorId)
            .IsRequired();

        builder.Property(e => e.StartedAtUtc)
            .IsRequired();

        builder.Property(e => e.Status)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.CurrentMetricValue)
            .HasPrecision(18, 4);

        builder.Property(e => e.BaselineMetricValue)
            .HasPrecision(18, 4);

        builder.Property(e => e.Findings)
            .HasMaxLength(4000);

        builder.Property(e => e.GeneratedInsights)
            .HasColumnType("jsonb");

        builder.Property(e => e.ErrorMessage)
            .HasMaxLength(2000);

        builder.Property(e => e.ExecutionDurationMs)
            .IsRequired();

        builder.HasIndex(e => new { e.MonitorId, e.StartedAtUtc })
            .HasDatabaseName("ix_monitor_executions_monitor_started");
    }
}

public sealed class AlertConfiguration : IEntityTypeConfiguration<Alert>
{
    public void Configure(EntityTypeBuilder<Alert> builder)
    {
        builder.ToTable("alerts");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.WorkspaceId)
            .IsRequired();

        builder.Property(a => a.AlertType)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(a => a.Severity)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(a => a.Title)
            .HasMaxLength(250)
            .IsRequired();

        builder.Property(a => a.Description)
            .HasMaxLength(4000)
            .IsRequired();

        builder.Property(a => a.MetricValue)
            .HasPrecision(18, 4);

        builder.Property(a => a.ThresholdValue)
            .HasPrecision(18, 4);

        builder.Property(a => a.IsAcknowledged)
            .IsRequired();

        builder.Property(a => a.CreatedAtUtc)
            .IsRequired();

        builder.HasIndex(a => new { a.WorkspaceId, a.IsAcknowledged })
            .HasDatabaseName("ix_alerts_workspace_acknowledged");

        builder.HasIndex(a => new { a.WorkspaceId, a.Severity })
            .HasDatabaseName("ix_alerts_workspace_severity");
    }
}

public sealed class ExecutiveReportConfiguration : IEntityTypeConfiguration<ExecutiveReport>
{
    public void Configure(EntityTypeBuilder<ExecutiveReport> builder)
    {
        builder.ToTable("executive_reports");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.WorkspaceId)
            .IsRequired();

        builder.Property(r => r.Title)
            .HasMaxLength(250)
            .IsRequired();

        builder.Property(r => r.ReportPeriod)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(r => r.Summary)
            .IsRequired();

        builder.Property(r => r.Findings)
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(r => r.Risks)
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(r => r.Opportunities)
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(r => r.Recommendations)
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(r => r.GeneratedAtUtc)
            .IsRequired();

        builder.HasIndex(r => new { r.WorkspaceId, r.GeneratedAtUtc })
            .HasDatabaseName("ix_executive_reports_workspace_generated");
    }
}
