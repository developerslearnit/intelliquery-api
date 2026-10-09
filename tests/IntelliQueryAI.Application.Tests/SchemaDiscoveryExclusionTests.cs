using FluentAssertions;
using IntelliQueryAI.Domain.Entities.DataSources;
using Xunit;

namespace IntelliQueryAI.Application.Tests;

public sealed class SchemaDiscoveryExclusionTests
{
    [Fact]
    public void SchemaMetadata_WhenFilteringTables_ShouldStrictlyExclude_EFMigrationsHistory_CaseInsensitively()
    {
        var workspaceId = Guid.NewGuid();
        var dataSourceId = Guid.NewGuid();

        var columns = new List<SchemaMetadata>
        {
            SchemaMetadata.Create(workspaceId, dataSourceId, "public", "users", "id", "uuid", false, true, false, null, "Users", "Id", 1),
            SchemaMetadata.Create(workspaceId, dataSourceId, "public", "users", "email", "varchar", false, false, false, null, "Users", "Email", 2),
            SchemaMetadata.Create(workspaceId, dataSourceId, "public", "__EFMigrationsHistory", "MigrationId", "varchar", false, true, false, null, "EF Migrations", "Migration Id", 1),
            SchemaMetadata.Create(workspaceId, dataSourceId, "public", "__efmigrationshistory", "ProductVersion", "varchar", false, false, false, null, "EF Migrations", "Product Version", 2)
        };

        var filtered = columns
            .Where(c => !string.Equals(c.TableName, "__EFMigrationsHistory", StringComparison.OrdinalIgnoreCase))
            .ToList();

        filtered.Should().HaveCount(2);
        filtered.Should().OnlyContain(c => c.TableName == "users");
    }

    [Fact]
    public void TableRelationships_WhenFilteringTables_ShouldStrictlyExclude_EFMigrationsHistory_CaseInsensitively()
    {
        var workspaceId = Guid.NewGuid();
        var dataSourceId = Guid.NewGuid();

        var relationships = new List<TableRelationship>
        {
            TableRelationship.Create(workspaceId, dataSourceId, "users", "id", "orders", "user_id", "fk_orders_user"),
            TableRelationship.Create(workspaceId, dataSourceId, "__EFMigrationsHistory", "id", "other", "id", "fk_fake"),
            TableRelationship.Create(workspaceId, dataSourceId, "other", "id", "__efmigrationshistory", "id", "fk_fake2")
        };

        var filtered = relationships
            .Where(r => !string.Equals(r.ParentTable, "__EFMigrationsHistory", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(r.ChildTable, "__EFMigrationsHistory", StringComparison.OrdinalIgnoreCase))
            .ToList();

        filtered.Should().HaveCount(1);
        filtered[0].ParentTable.Should().Be("users");
    }

    [Fact]
    public void DatabaseRoutines_WhenFilteringTables_ShouldStrictlyExclude_EFMigrationsHistory_CaseInsensitively()
    {
        var workspaceId = Guid.NewGuid();
        var dataSourceId = Guid.NewGuid();

        var routines = new List<DatabaseRoutine>
        {
            DatabaseRoutine.Create(workspaceId, dataSourceId, "get_users", "PROCEDURE", null, "Get Users"),
            DatabaseRoutine.Create(workspaceId, dataSourceId, "__EFMigrationsHistory", "VIEW", null, "Migration View")
        };

        var filtered = routines
            .Where(r => !string.Equals(r.RoutineName, "__EFMigrationsHistory", StringComparison.OrdinalIgnoreCase))
            .ToList();

        filtered.Should().HaveCount(1);
        filtered[0].RoutineName.Should().Be("get_users");
    }
}
