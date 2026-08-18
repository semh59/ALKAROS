-- ============================================================================
-- Migration: 026-typed-settings.up.sql
-- Task: V1-SET-001 (Implement typed module-owned settings)
-- Specification: PDF:III.27 (III.27.1 settings, III.27.2 setting_history), V0-ARC-005
-- ============================================================================

CREATE SCHEMA IF NOT EXISTS settings;

-- Table: settings.settings
CREATE TABLE IF NOT EXISTS settings.settings (
    setting_id UUID PRIMARY KEY,
    setting_key TEXT NOT NULL,
    setting_value TEXT NOT NULL,
    data_type TEXT NOT NULL,
    scope TEXT NOT NULL,
    module_owner TEXT NOT NULL,
    description TEXT NULL,
    requires_restart BOOLEAN NOT NULL DEFAULT FALSE,
    active BOOLEAN NOT NULL DEFAULT TRUE,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    row_version BIGINT NOT NULL DEFAULT 1,
    CONSTRAINT uq_settings_key UNIQUE (setting_key),
    CONSTRAINT chk_settings_data_type CHECK (data_type IN ('String', 'Integer', 'Decimal', 'Boolean', 'Json', 'Duration')),
    CONSTRAINT chk_settings_scope CHECK (scope IN ('Global', 'Module', 'Device', 'Tenant')),
    CONSTRAINT chk_settings_row_version CHECK (row_version >= 1)
);

CREATE INDEX IF NOT EXISTS idx_settings_module_owner ON settings.settings(module_owner);
CREATE INDEX IF NOT EXISTS idx_settings_scope ON settings.settings(scope);
CREATE INDEX IF NOT EXISTS idx_settings_active ON settings.settings(active);

-- Table: settings.setting_history (Append-only audit trail)
CREATE TABLE IF NOT EXISTS settings.setting_history (
    setting_history_id UUID PRIMARY KEY,
    setting_id UUID NOT NULL REFERENCES settings.settings(setting_id) ON DELETE CASCADE,
    old_value TEXT NULL,
    new_value TEXT NOT NULL,
    reason TEXT NULL,
    changed_by UUID NULL REFERENCES identity.users(user_id) ON DELETE SET NULL,
    changed_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_setting_history_setting_id ON settings.setting_history(setting_id, changed_at DESC);
CREATE INDEX IF NOT EXISTS idx_setting_history_changed_at ON settings.setting_history(changed_at DESC);
