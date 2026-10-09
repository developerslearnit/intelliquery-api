-- Migration: Phase G - What-If & Scenario Analysis
-- Table: scenarios

CREATE TABLE IF NOT EXISTS scenarios (
    id UUID PRIMARY KEY,
    workspace_id UUID NOT NULL,
    created_by_user_id UUID NOT NULL,
    title VARCHAR(250) NOT NULL,
    description VARCHAR(1000),
    target_metric VARCHAR(150) NOT NULL,
    baseline_value NUMERIC(18, 2) NOT NULL,
    time_horizon VARCHAR(100) NOT NULL DEFAULT 'Next 12 Months',
    variables_json JSONB NOT NULL DEFAULT '[]'::jsonb,
    projections_json JSONB NOT NULL DEFAULT '[]'::jsonb,
    ai_explanation TEXT,
    confidence_score DOUBLE PRECISION NOT NULL DEFAULT 0.85,
    created_at_utc TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at_utc TIMESTAMPTZ
);

CREATE INDEX IF NOT EXISTS ix_scenarios_workspace_created ON scenarios (workspace_id, created_at_utc DESC);
CREATE INDEX IF NOT EXISTS ix_scenarios_metric ON scenarios (workspace_id, target_metric);
