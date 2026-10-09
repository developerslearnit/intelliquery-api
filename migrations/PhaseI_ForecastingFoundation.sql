-- Phase I: Forecasting Foundation Migration
-- Table: forecasts

CREATE TABLE IF NOT EXISTS forecasts (
    id UUID PRIMARY KEY,
    workspace_id UUID NOT NULL,
    created_by_user_id UUID NULL,
    data_source_id UUID NULL,
    metric_name VARCHAR(256) NOT NULL,
    forecast_period VARCHAR(128) NOT NULL,
    historical_period VARCHAR(128) NOT NULL,
    method VARCHAR(64) NOT NULL,
    confidence_interval DOUBLE PRECISION NOT NULL DEFAULT 0.95,
    historical_data_points_json TEXT NOT NULL DEFAULT '[]',
    forecast_data_points_json TEXT NOT NULL DEFAULT '[]',
    assumptions_json TEXT NOT NULL DEFAULT '[]',
    ai_explanation TEXT NULL,
    projected_growth_rate NUMERIC(18, 4) NOT NULL DEFAULT 0,
    final_projected_value NUMERIC(18, 4) NOT NULL DEFAULT 0,
    created_at_utc TIMESTAMPTZ NOT NULL,
    updated_at_utc TIMESTAMPTZ NULL
);

CREATE INDEX IF NOT EXISTS ix_forecasts_workspace_metric ON forecasts (workspace_id, metric_name);
CREATE INDEX IF NOT EXISTS ix_forecasts_workspace_created ON forecasts (workspace_id, created_at_utc DESC);
