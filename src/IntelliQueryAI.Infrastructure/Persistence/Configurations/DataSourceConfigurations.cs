using IntelliQueryAI.Domain.Entities.DataSources;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IntelliQueryAI.Infrastructure.Persistence.Configurations;

public sealed class DataSourceConfiguration : IEntityTypeConfiguration<DataSource>
{
    public void Configure(EntityTypeBuilder<DataSource> builder)
    {
        builder.ToTable("data_sources");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.WorkspaceId).IsRequired();
        builder.HasIndex(d => d.WorkspaceId);

        builder.Property(d => d.Name)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(d => d.DatabaseType)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(d => d.EncryptedConnectionString)
            .IsRequired();

        builder.Property(d => d.IsActive).IsRequired();

        builder.Property(d => d.SyncStatus)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(d => d.SyncErrorMessage)
            .HasMaxLength(2000);

        builder.Property(d => d.CreatedAtUtc).IsRequired();
    }
}

public sealed class SchemaMetadataConfiguration : IEntityTypeConfiguration<SchemaMetadata>
{
    public void Configure(EntityTypeBuilder<SchemaMetadata> builder)
    {
        builder.ToTable("schema_metadata");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.WorkspaceId).IsRequired();
        builder.Property(s => s.DataSourceId).IsRequired();

        builder.Property(s => s.SchemaName)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(s => s.TableName)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(s => s.ColumnName)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(s => s.DataType)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(s => s.FriendlyTableName)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(s => s.FriendlyColumnName)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(s => s.Description)
            .HasMaxLength(2000);

        builder.Property(s => s.DiscoveredAtUtc).IsRequired();

        // Optimized PostgreSQL composite indexes for multi-tenant high-volume reads
        builder.HasIndex(s => new { s.WorkspaceId, s.DataSourceId, s.TableName });
        builder.HasIndex(s => new { s.WorkspaceId, s.TableName });
        builder.HasIndex(s => s.DataSourceId);
    }
}

public sealed class TableRelationshipConfiguration : IEntityTypeConfiguration<TableRelationship>
{
    public void Configure(EntityTypeBuilder<TableRelationship> builder)
    {
        builder.ToTable("table_relationships");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.WorkspaceId).IsRequired();
        builder.Property(r => r.DataSourceId).IsRequired();

        builder.Property(r => r.ParentTable)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(r => r.ParentColumn)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(r => r.ChildTable)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(r => r.ChildColumn)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(r => r.ConstraintName)
            .HasMaxLength(256);

        builder.Property(r => r.DiscoveredAtUtc).IsRequired();

        builder.HasIndex(r => new { r.WorkspaceId, r.DataSourceId });
        builder.HasIndex(r => new { r.ParentTable, r.ChildTable });
    }
}

public sealed class DatabaseRoutineConfiguration : IEntityTypeConfiguration<DatabaseRoutine>
{
    public void Configure(EntityTypeBuilder<DatabaseRoutine> builder)
    {
        builder.ToTable("database_routines");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.WorkspaceId).IsRequired();
        builder.Property(r => r.DataSourceId).IsRequired();

        builder.Property(r => r.RoutineName)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(r => r.RoutineType)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(r => r.FriendlyName)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(r => r.DiscoveredAtUtc).IsRequired();

        builder.HasIndex(r => new { r.WorkspaceId, r.DataSourceId });
    }
}
