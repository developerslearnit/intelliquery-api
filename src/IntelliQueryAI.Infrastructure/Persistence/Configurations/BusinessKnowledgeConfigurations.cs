using IntelliQueryAI.Domain.Entities.BusinessKnowledge;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IntelliQueryAI.Infrastructure.Persistence.Configurations;

public sealed class BusinessTermConfiguration : IEntityTypeConfiguration<BusinessTerm>
{
    public void Configure(EntityTypeBuilder<BusinessTerm> builder)
    {
        builder.ToTable("business_terms");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.WorkspaceId)
            .IsRequired();

        builder.Property(t => t.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(t => t.Definition)
            .HasMaxLength(2000)
            .IsRequired();

        builder.Property(t => t.AliasesJson)
            .HasColumnType("jsonb")
            .HasDefaultValue("[]")
            .IsRequired();

        builder.Property(t => t.Category)
            .HasMaxLength(100)
            .HasDefaultValue("General")
            .IsRequired();

        builder.Property(t => t.Classification)
            .HasMaxLength(100)
            .HasDefaultValue("Internal")
            .IsRequired();

        builder.Property(t => t.OwnerName)
            .HasMaxLength(200);

        builder.Property(t => t.OwnerEmail)
            .HasMaxLength(256);

        builder.Property(t => t.TargetTable)
            .HasMaxLength(200);

        builder.Property(t => t.TargetColumn)
            .HasMaxLength(200);

        builder.Property(t => t.SqlCalculationRule)
            .HasMaxLength(2000);

        builder.Property(t => t.IsActive)
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(t => t.CreatedAtUtc)
            .IsRequired();

        builder.HasIndex(t => new { t.WorkspaceId, t.Name })
            .HasDatabaseName("ix_business_terms_workspace_name");

        builder.HasIndex(t => new { t.WorkspaceId, t.Category })
            .HasDatabaseName("ix_business_terms_workspace_category");
    }
}

public sealed class MetricDefinitionConfiguration : IEntityTypeConfiguration<MetricDefinition>
{
    public void Configure(EntityTypeBuilder<MetricDefinition> builder)
    {
        builder.ToTable("metric_definitions");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.WorkspaceId)
            .IsRequired();

        builder.Property(m => m.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(m => m.Description)
            .HasMaxLength(2000)
            .IsRequired();

        builder.Property(m => m.Unit)
            .HasMaxLength(50);

        builder.Property(m => m.CalculationFormula)
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(m => m.FilterExpression)
            .HasMaxLength(2000);

        builder.Property(m => m.TargetTable)
            .HasMaxLength(200);

        builder.Property(m => m.TargetColumn)
            .HasMaxLength(200);

        builder.Property(m => m.AggregationType)
            .HasMaxLength(50)
            .HasDefaultValue("SUM")
            .IsRequired();

        builder.Property(m => m.TrendDirection)
            .HasMaxLength(50)
            .HasDefaultValue("HigherIsBetter")
            .IsRequired();

        builder.Property(m => m.IsActive)
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(m => m.CreatedAtUtc)
            .IsRequired();

        builder.HasIndex(m => new { m.WorkspaceId, m.Name })
            .HasDatabaseName("ix_metric_definitions_workspace_name");
    }
}
