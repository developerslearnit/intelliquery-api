using IntelliQueryAI.Domain.Entities.Dashboards;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IntelliQueryAI.Infrastructure.Persistence.Configurations;

public sealed class CustomDashboardConfiguration : IEntityTypeConfiguration<CustomDashboard>
{
    public void Configure(EntityTypeBuilder<CustomDashboard> builder)
    {
        builder.ToTable("custom_dashboards");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.WorkspaceId)
            .IsRequired();

        builder.Property(d => d.CreatedByUserId)
            .IsRequired();

        builder.Property(d => d.Title)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(d => d.Description)
            .HasMaxLength(1000);

        builder.Property(d => d.Prompt)
            .HasMaxLength(2000);

        builder.Property(d => d.LayoutJson)
            .HasColumnType("jsonb")
            .HasDefaultValue("[]")
            .IsRequired();

        builder.Property(d => d.DateRange)
            .HasMaxLength(100)
            .HasDefaultValue("Last 30 Days")
            .IsRequired();

        builder.Property(d => d.IsDefault)
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(d => d.IsShared)
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(d => d.CreatedAtUtc)
            .IsRequired();

        builder.Property(d => d.UpdatedAtUtc);

        builder.HasIndex(d => new { d.WorkspaceId, d.CreatedAtUtc })
            .HasDatabaseName("ix_custom_dashboards_workspace_created");
    }
}
