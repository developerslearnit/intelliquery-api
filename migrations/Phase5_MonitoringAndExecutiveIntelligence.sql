START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261006131123_Phase5_MonitoringAndExecutiveIntelligence') THEN
    CREATE TABLE alerts (
        "Id" uuid NOT NULL,
        "WorkspaceId" uuid NOT NULL,
        "MonitorId" uuid,
        "AlertType" character varying(50) NOT NULL,
        "Severity" character varying(50) NOT NULL,
        "Title" character varying(250) NOT NULL,
        "Description" character varying(4000) NOT NULL,
        "MetricValue" numeric(18,4),
        "ThresholdValue" numeric(18,4),
        "IsAcknowledged" boolean NOT NULL,
        "AcknowledgedAtUtc" timestamp with time zone,
        "AcknowledgedByUserId" uuid,
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_alerts" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261006131123_Phase5_MonitoringAndExecutiveIntelligence') THEN
    CREATE TABLE executive_reports (
        "Id" uuid NOT NULL,
        "WorkspaceId" uuid NOT NULL,
        "Title" character varying(250) NOT NULL,
        "ReportPeriod" character varying(100) NOT NULL,
        "Summary" text NOT NULL,
        "Findings" jsonb NOT NULL,
        "Risks" jsonb NOT NULL,
        "Opportunities" jsonb NOT NULL,
        "Recommendations" jsonb NOT NULL,
        "GeneratedAtUtc" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_executive_reports" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261006131123_Phase5_MonitoringAndExecutiveIntelligence') THEN
    CREATE TABLE monitors (
        "Id" uuid NOT NULL,
        "WorkspaceId" uuid NOT NULL,
        "DataSourceId" uuid NOT NULL,
        "Name" character varying(200) NOT NULL,
        "Description" character varying(1000),
        "MonitorType" character varying(50) NOT NULL,
        "IsActive" boolean NOT NULL,
        "ScheduleType" character varying(50) NOT NULL,
        "QueryTemplate" character varying(4000),
        "TargetMetric" character varying(200),
        "LastExecutedAtUtc" timestamp with time zone,
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        "UpdatedAtUtc" timestamp with time zone,
        CONSTRAINT "PK_monitors" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261006131123_Phase5_MonitoringAndExecutiveIntelligence') THEN
    CREATE TABLE monitor_executions (
        "Id" uuid NOT NULL,
        "MonitorId" uuid NOT NULL,
        "StartedAtUtc" timestamp with time zone NOT NULL,
        "CompletedAtUtc" timestamp with time zone,
        "Status" character varying(50) NOT NULL,
        "CurrentMetricValue" numeric(18,4),
        "BaselineMetricValue" numeric(18,4),
        "Findings" character varying(4000),
        "GeneratedInsights" jsonb,
        "ErrorMessage" character varying(2000),
        "ExecutionDurationMs" bigint NOT NULL,
        CONSTRAINT "PK_monitor_executions" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_monitor_executions_monitors_MonitorId" FOREIGN KEY ("MonitorId") REFERENCES monitors ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261006131123_Phase5_MonitoringAndExecutiveIntelligence') THEN
    CREATE TABLE monitor_rules (
        "Id" uuid NOT NULL,
        "MonitorId" uuid NOT NULL,
        "RuleType" character varying(50) NOT NULL,
        "ThresholdValue" numeric(18,4) NOT NULL,
        "ComparisonOperator" character varying(50) NOT NULL,
        "TimeWindow" character varying(50),
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_monitor_rules" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_monitor_rules_monitors_MonitorId" FOREIGN KEY ("MonitorId") REFERENCES monitors ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261006131123_Phase5_MonitoringAndExecutiveIntelligence') THEN
    CREATE INDEX ix_alerts_workspace_acknowledged ON alerts ("WorkspaceId", "IsAcknowledged");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261006131123_Phase5_MonitoringAndExecutiveIntelligence') THEN
    CREATE INDEX ix_alerts_workspace_severity ON alerts ("WorkspaceId", "Severity");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261006131123_Phase5_MonitoringAndExecutiveIntelligence') THEN
    CREATE INDEX ix_executive_reports_workspace_generated ON executive_reports ("WorkspaceId", "GeneratedAtUtc");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261006131123_Phase5_MonitoringAndExecutiveIntelligence') THEN
    CREATE INDEX ix_monitor_executions_monitor_started ON monitor_executions ("MonitorId", "StartedAtUtc");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261006131123_Phase5_MonitoringAndExecutiveIntelligence') THEN
    CREATE INDEX ix_monitor_rules_monitor_id ON monitor_rules ("MonitorId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261006131123_Phase5_MonitoringAndExecutiveIntelligence') THEN
    CREATE INDEX ix_monitors_schedule_active ON monitors ("ScheduleType", "IsActive");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261006131123_Phase5_MonitoringAndExecutiveIntelligence') THEN
    CREATE INDEX ix_monitors_workspace_active ON monitors ("WorkspaceId", "IsActive");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261006131123_Phase5_MonitoringAndExecutiveIntelligence') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261006131123_Phase5_MonitoringAndExecutiveIntelligence', '10.0.12');
    END IF;
END $EF$;
COMMIT;

