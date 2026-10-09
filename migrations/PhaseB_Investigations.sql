-- Phase B: AI Root-Cause Investigation Migration
CREATE TABLE IF NOT EXISTS public.investigation_reports (
    "Id" uuid NOT NULL,
    "WorkspaceId" uuid NOT NULL,
    "DataSourceId" uuid,
    "Question" character varying(2000) NOT NULL,
    "TargetMetric" character varying(200),
    "Summary" text NOT NULL,
    "ConfidenceScore" double precision NOT NULL,
    "Status" character varying(50) NOT NULL DEFAULT 'Completed',
    "RootCausesJson" jsonb NOT NULL DEFAULT '[]'::jsonb,
    "ContributorsJson" jsonb NOT NULL DEFAULT '[]'::jsonb,
    "EvidenceJson" jsonb NOT NULL DEFAULT '[]'::jsonb,
    "RecommendationsJson" jsonb NOT NULL DEFAULT '[]'::jsonb,
    "QueriesExecutedJson" jsonb NOT NULL DEFAULT '[]'::jsonb,
    "FollowUpQuestionsJson" jsonb NOT NULL DEFAULT '[]'::jsonb,
    "DurationMs" bigint NOT NULL,
    "TokensUsed" integer NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_investigation_reports" PRIMARY KEY ("Id")
);

CREATE INDEX IF NOT EXISTS ix_investigation_reports_workspace_created ON public.investigation_reports ("WorkspaceId", "CreatedAtUtc" DESC);
