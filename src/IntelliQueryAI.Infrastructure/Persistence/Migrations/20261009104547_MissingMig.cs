using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IntelliQueryAI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MissingMig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "business_terms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Definition = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    AliasesJson = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "[]"),
                    Category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, defaultValue: "General"),
                    Classification = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, defaultValue: "Internal"),
                    OwnerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    OwnerEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    TargetTable = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    TargetColumn = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SqlCalculationRule = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_business_terms", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "custom_dashboards",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Prompt = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    LayoutJson = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "[]"),
                    DateRange = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, defaultValue: "Last 30 Days"),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    IsShared = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_custom_dashboards", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "forecasts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    workspace_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    data_source_id = table.Column<Guid>(type: "uuid", nullable: true),
                    metric_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    forecast_period = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    historical_period = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    method = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    confidence_interval = table.Column<double>(type: "double precision", nullable: false),
                    historical_data_points_json = table.Column<string>(type: "text", nullable: false),
                    forecast_data_points_json = table.Column<string>(type: "text", nullable: false),
                    assumptions_json = table.Column<string>(type: "text", nullable: false),
                    ai_explanation = table.Column<string>(type: "text", nullable: true),
                    projected_growth_rate = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    final_projected_value = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_forecasts", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "governance_policies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    TargetTable = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, defaultValue: "*"),
                    TargetColumn = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, defaultValue: "*"),
                    Classification = table.Column<int>(type: "integer", nullable: false),
                    MaskingStrategy = table.Column<int>(type: "integer", nullable: false),
                    MaskingPattern = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AllowedRolesJson = table.Column<string>(type: "text", nullable: false, defaultValue: "[\"Owner\",\"Admin\"]"),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    IsAiMaskingEnforced = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_governance_policies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "investigation_reports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    DataSourceId = table.Column<Guid>(type: "uuid", nullable: true),
                    Question = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    TargetMetric = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Summary = table.Column<string>(type: "text", nullable: false),
                    ConfidenceScore = table.Column<double>(type: "double precision", nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "Completed"),
                    RootCausesJson = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "[]"),
                    ContributorsJson = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "[]"),
                    EvidenceJson = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "[]"),
                    RecommendationsJson = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "[]"),
                    QueriesExecutedJson = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "[]"),
                    FollowUpQuestionsJson = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "[]"),
                    DurationMs = table.Column<long>(type: "bigint", nullable: false),
                    TokensUsed = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_investigation_reports", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "metric_definitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    DataSourceId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Unit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CalculationFormula = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    FilterExpression = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    TargetTable = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    TargetColumn = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AggregationType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "SUM"),
                    TrendDirection = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "HigherIsBetter"),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_metric_definitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "recommendations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    workspace_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    rationale = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    category = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    priority = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    target_metric = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    impact_magnitude = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    estimated_effort = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    evidence_json = table.Column<string>(type: "text", nullable: false),
                    action_steps_json = table.Column<string>(type: "text", nullable: false),
                    related_investigation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    related_alert_id = table.Column<Guid>(type: "uuid", nullable: true),
                    related_monitor_id = table.Column<Guid>(type: "uuid", nullable: true),
                    related_scenario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    applied_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    dismissed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    dismissal_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recommendations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "scenarios",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    workspace_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    target_metric = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    baseline_value = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    time_horizon = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    variables_json = table.Column<string>(type: "jsonb", nullable: false),
                    projections_json = table.Column<string>(type: "jsonb", nullable: false),
                    ai_explanation = table.Column<string>(type: "text", nullable: true),
                    confidence_score = table.Column<double>(type: "double precision", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_scenarios", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_business_terms_workspace_category",
                table: "business_terms",
                columns: new[] { "WorkspaceId", "Category" });

            migrationBuilder.CreateIndex(
                name: "ix_business_terms_workspace_name",
                table: "business_terms",
                columns: new[] { "WorkspaceId", "Name" });

            migrationBuilder.CreateIndex(
                name: "ix_custom_dashboards_workspace_created",
                table: "custom_dashboards",
                columns: new[] { "WorkspaceId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "ix_forecasts_workspace_created",
                table: "forecasts",
                columns: new[] { "workspace_id", "created_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_forecasts_workspace_metric",
                table: "forecasts",
                columns: new[] { "workspace_id", "metric_name" });

            migrationBuilder.CreateIndex(
                name: "ix_governance_policies_target",
                table: "governance_policies",
                columns: new[] { "WorkspaceId", "TargetTable", "TargetColumn" });

            migrationBuilder.CreateIndex(
                name: "ix_governance_policies_workspace_active",
                table: "governance_policies",
                columns: new[] { "WorkspaceId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "ix_investigation_reports_workspace_created",
                table: "investigation_reports",
                columns: new[] { "WorkspaceId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "ix_metric_definitions_workspace_name",
                table: "metric_definitions",
                columns: new[] { "WorkspaceId", "Name" });

            migrationBuilder.CreateIndex(
                name: "ix_recommendations_workspace_created",
                table: "recommendations",
                columns: new[] { "workspace_id", "created_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_recommendations_workspace_priority",
                table: "recommendations",
                columns: new[] { "workspace_id", "priority" });

            migrationBuilder.CreateIndex(
                name: "ix_recommendations_workspace_status",
                table: "recommendations",
                columns: new[] { "workspace_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_scenarios_metric",
                table: "scenarios",
                columns: new[] { "workspace_id", "target_metric" });

            migrationBuilder.CreateIndex(
                name: "ix_scenarios_workspace_created",
                table: "scenarios",
                columns: new[] { "workspace_id", "created_at_utc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "business_terms");

            migrationBuilder.DropTable(
                name: "custom_dashboards");

            migrationBuilder.DropTable(
                name: "forecasts");

            migrationBuilder.DropTable(
                name: "governance_policies");

            migrationBuilder.DropTable(
                name: "investigation_reports");

            migrationBuilder.DropTable(
                name: "metric_definitions");

            migrationBuilder.DropTable(
                name: "recommendations");

            migrationBuilder.DropTable(
                name: "scenarios");
        }
    }
}
