using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IntelliQueryAI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDataSourcesAndSchemaDiscovery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "data_sources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    DatabaseType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    EncryptedConnectionString = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    LastSyncAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SyncStatus = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SyncErrorMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_data_sources", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "database_routines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    DataSourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoutineName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    RoutineType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Definition = table.Column<string>(type: "text", nullable: true),
                    FriendlyName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    DiscoveredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_database_routines", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "schema_metadata",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    DataSourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    SchemaName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    TableName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ColumnName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    DataType = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    IsNullable = table.Column<bool>(type: "boolean", nullable: false),
                    IsPrimaryKey = table.Column<bool>(type: "boolean", nullable: false),
                    IsForeignKey = table.Column<bool>(type: "boolean", nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    FriendlyTableName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    FriendlyColumnName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    OrdinalPosition = table.Column<int>(type: "integer", nullable: false),
                    DiscoveredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_schema_metadata", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "table_relationships",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    DataSourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParentTable = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ParentColumn = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ChildTable = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ChildColumn = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ConstraintName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    DiscoveredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_table_relationships", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_data_sources_WorkspaceId",
                table: "data_sources",
                column: "WorkspaceId");

            migrationBuilder.CreateIndex(
                name: "IX_database_routines_WorkspaceId_DataSourceId",
                table: "database_routines",
                columns: new[] { "WorkspaceId", "DataSourceId" });

            migrationBuilder.CreateIndex(
                name: "IX_schema_metadata_DataSourceId",
                table: "schema_metadata",
                column: "DataSourceId");

            migrationBuilder.CreateIndex(
                name: "IX_schema_metadata_WorkspaceId_DataSourceId_TableName",
                table: "schema_metadata",
                columns: new[] { "WorkspaceId", "DataSourceId", "TableName" });

            migrationBuilder.CreateIndex(
                name: "IX_schema_metadata_WorkspaceId_TableName",
                table: "schema_metadata",
                columns: new[] { "WorkspaceId", "TableName" });

            migrationBuilder.CreateIndex(
                name: "IX_table_relationships_ParentTable_ChildTable",
                table: "table_relationships",
                columns: new[] { "ParentTable", "ChildTable" });

            migrationBuilder.CreateIndex(
                name: "IX_table_relationships_WorkspaceId_DataSourceId",
                table: "table_relationships",
                columns: new[] { "WorkspaceId", "DataSourceId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "data_sources");

            migrationBuilder.DropTable(
                name: "database_routines");

            migrationBuilder.DropTable(
                name: "schema_metadata");

            migrationBuilder.DropTable(
                name: "table_relationships");
        }
    }
}
