-- Phase F: Enterprise Data Governance Migration
-- Creates governance_policies table for column-level sensitivity classification and masking policies

CREATE TABLE IF NOT EXISTS governance_policies (
    id UUID PRIMARY KEY,
    workspace_id UUID NOT NULL,
    name VARCHAR(200) NOT NULL,
    description VARCHAR(1000),
    target_table VARCHAR(100) NOT NULL DEFAULT '*',
    target_column VARCHAR(100) NOT NULL DEFAULT '*',
    classification INT NOT NULL DEFAULT 1,
    masking_strategy INT NOT NULL DEFAULT 1,
    masking_pattern VARCHAR(200),
    allowed_roles_json TEXT NOT NULL DEFAULT '["Owner","Admin"]',
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    is_ai_masking_enforced BOOLEAN NOT NULL DEFAULT TRUE,
    created_at_utc TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at_utc TIMESTAMPTZ
);

CREATE INDEX IF NOT EXISTS ix_governance_policies_workspace_active 
    ON governance_policies (workspace_id, is_active);

CREATE INDEX IF NOT EXISTS ix_governance_policies_target 
    ON governance_policies (workspace_id, target_table, target_column);
