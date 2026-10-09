using IntelliQueryAI.Domain.Entities.Governance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IntelliQueryAI.Infrastructure.Persistence.Configurations;

public sealed class GovernancePolicyConfiguration : IEntityTypeConfiguration<GovernancePolicy>
{
    public void Configure(EntityTypeBuilder<GovernancePolicy> builder)
    {
        builder.ToTable("governance_policies");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.WorkspaceId)
            .IsRequired();

        builder.Property(p => p.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(p => p.Description)
            .HasMaxLength(1000);

        builder.Property(p => p.TargetTable)
            .HasMaxLength(100)
            .HasDefaultValue("*")
            .IsRequired();

        builder.Property(p => p.TargetColumn)
            .HasMaxLength(100)
            .HasDefaultValue("*")
            .IsRequired();

        builder.Property(p => p.Classification)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(p => p.MaskingStrategy)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(p => p.MaskingPattern)
            .HasMaxLength(200);

        builder.Property(p => p.AllowedRolesJson)
            .HasColumnType("text")
            .HasDefaultValue("[\"Owner\",\"Admin\"]")
            .IsRequired();

        builder.Property(p => p.IsActive)
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(p => p.IsAiMaskingEnforced)
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(p => p.CreatedAtUtc)
            .IsRequired();

        builder.Property(p => p.UpdatedAtUtc);

        builder.HasIndex(p => new { p.WorkspaceId, p.IsActive })
            .HasDatabaseName("ix_governance_policies_workspace_active");

        builder.HasIndex(p => new { p.WorkspaceId, p.TargetTable, p.TargetColumn })
            .HasDatabaseName("ix_governance_policies_target");
    }
}
