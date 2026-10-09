-- Phase H: Recommendation Engine Migration
-- Table: recommendations

CREATE TABLE IF NOT EXISTS recommendations (
    id UUID PRIMARY KEY,
    workspace_id UUID NOT NULL,
    created_by_user_id UUID NULL,
    title VARCHAR(256) NOT NULL,
    rationale VARCHAR(4000) NOT NULL,
    category VARCHAR(128) NOT NULL,
    priority VARCHAR(32) NOT NULL,
    status VARCHAR(32) NOT NULL,
    target_metric VARCHAR(256) NULL,
    impact_magnitude VARCHAR(256) NULL,
    estimated_effort VARCHAR(128) NULL,
    evidence_json TEXT NOT NULL DEFAULT '[]',
    action_steps_json TEXT NOT NULL DEFAULT '[]',
    related_investigation_id UUID NULL,
    related_alert_id UUID NULL,
    related_monitor_id UUID NULL,
    related_scenario_id UUID NULL,
    applied_at_utc TIMESTAMPTZ NULL,
    dismissed_at_utc TIMESTAMPTZ NULL,
    dismissal_reason VARCHAR(1000) NULL,
    created_at_utc TIMESTAMPTZ NOT NULL,
    updated_at_utc TIMESTAMPTZ NULL
);

CREATE INDEX IF NOT EXISTS ix_recommendations_workspace_status ON recommendations (workspace_id, status);
CREATE INDEX IF NOT EXISTS ix_recommendations_workspace_created ON recommendations (workspace_id, created_at_utc DESC);
CREATE INDEX IF NOT EXISTS ix_recommendations_workspace_priority ON recommendations (workspace_id, priority);
