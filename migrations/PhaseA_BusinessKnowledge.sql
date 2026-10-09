-- Phase A: Business Knowledge / Semantic Layer Migration
CREATE TABLE IF NOT EXISTS public.business_terms (
    "Id" uuid NOT NULL,
    "WorkspaceId" uuid NOT NULL,
    "Name" character varying(200) NOT NULL,
    "Definition" character varying(2000) NOT NULL,
    "AliasesJson" jsonb NOT NULL DEFAULT '[]'::jsonb,
    "Category" character varying(100) NOT NULL DEFAULT 'General',
    "Classification" character varying(100) NOT NULL DEFAULT 'Internal',
    "OwnerName" character varying(200),
    "OwnerEmail" character varying(256),
    "TargetTable" character varying(200),
    "TargetColumn" character varying(200),
    "SqlCalculationRule" character varying(2000),
    "IsActive" boolean NOT NULL DEFAULT true,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "UpdatedAtUtc" timestamp with time zone,
    CONSTRAINT "PK_business_terms" PRIMARY KEY ("Id")
);

CREATE INDEX IF NOT EXISTS ix_business_terms_workspace_name ON public.business_terms ("WorkspaceId", "Name");
CREATE INDEX IF NOT EXISTS ix_business_terms_workspace_category ON public.business_terms ("WorkspaceId", "Category");

CREATE TABLE IF NOT EXISTS public.metric_definitions (
    "Id" uuid NOT NULL,
    "WorkspaceId" uuid NOT NULL,
    "DataSourceId" uuid,
    "Name" character varying(200) NOT NULL,
    "Description" character varying(2000) NOT NULL,
    "Unit" character varying(50),
    "CalculationFormula" character varying(1000) NOT NULL,
    "FilterExpression" character varying(2000),
    "TargetTable" character varying(200),
    "TargetColumn" character varying(200),
    "AggregationType" character varying(50) NOT NULL DEFAULT 'SUM',
    "TrendDirection" character varying(50) NOT NULL DEFAULT 'HigherIsBetter',
    "IsActive" boolean NOT NULL DEFAULT true,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "UpdatedAtUtc" timestamp with time zone,
    CONSTRAINT "PK_metric_definitions" PRIMARY KEY ("Id")
);

CREATE INDEX IF NOT EXISTS ix_metric_definitions_workspace_name ON public.metric_definitions ("WorkspaceId", "Name");
