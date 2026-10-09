-- Phase E: AI Custom Dashboard Generator Migration
CREATE TABLE IF NOT EXISTS public.custom_dashboards (
    "Id" uuid NOT NULL,
    "WorkspaceId" uuid NOT NULL,
    "CreatedByUserId" uuid NOT NULL,
    "Title" character varying(200) NOT NULL,
    "Description" character varying(1000),
    "Prompt" character varying(2000),
    "LayoutJson" jsonb NOT NULL DEFAULT '[]'::jsonb,
    "DateRange" character varying(100) NOT NULL DEFAULT 'Last 30 Days',
    "IsDefault" boolean NOT NULL DEFAULT false,
    "IsShared" boolean NOT NULL DEFAULT true,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "UpdatedAtUtc" timestamp with time zone,
    CONSTRAINT "PK_custom_dashboards" PRIMARY KEY ("Id")
);

CREATE INDEX IF NOT EXISTS ix_custom_dashboards_workspace_created ON public.custom_dashboards ("WorkspaceId", "CreatedAtUtc" DESC);
