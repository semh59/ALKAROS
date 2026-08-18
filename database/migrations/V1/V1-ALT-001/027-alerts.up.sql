-- ============================================================================
-- Migration: 027-alerts.up.sql
-- Task: V1-ALT-001 (Implement Alert foundation)
-- Specification: PDF:III.28 (III.28.2 alerts, III.28.3 alert_events), II.5.13, V0-DAT-002
-- ============================================================================

CREATE SCHEMA IF NOT EXISTS observability;

-- Table: observability.alerts
CREATE TABLE IF NOT EXISTS observability.alerts (
    alert_id UUID PRIMARY KEY,
    alert_type TEXT NOT NULL,
    severity TEXT NOT NULL,
    status TEXT NOT NULL,
    title TEXT NOT NULL,
    message TEXT NOT NULL,
    deduplication_key TEXT NULL,
    source_reference_type TEXT NULL,
    source_reference_id UUID NULL,
    opened_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    acknowledged_at TIMESTAMPTZ NULL,
    acknowledged_by UUID NULL REFERENCES identity.users(user_id) ON DELETE SET NULL,
    resolved_at TIMESTAMPTZ NULL,
    resolved_by UUID NULL REFERENCES identity.users(user_id) ON DELETE SET NULL,
    resolution_reason TEXT NULL,
    row_version BIGINT NOT NULL DEFAULT 1,
    CONSTRAINT chk_alerts_severity CHECK (severity IN ('Info', 'Warning', 'Critical')),
    CONSTRAINT chk_alerts_status CHECK (status IN ('Open', 'Acknowledged', 'Escalated', 'Suppressed', 'Resolved')),
    CONSTRAINT chk_alerts_row_version CHECK (row_version >= 1)
);

CREATE INDEX IF NOT EXISTS idx_alerts_status ON observability.alerts(status);
CREATE INDEX IF NOT EXISTS idx_alerts_severity ON observability.alerts(severity);
CREATE INDEX IF NOT EXISTS idx_alerts_dedup_active ON observability.alerts(deduplication_key)
    WHERE status IN ('Open', 'Acknowledged', 'Escalated');
CREATE INDEX IF NOT EXISTS idx_alerts_source_ref_active ON observability.alerts(source_reference_type, source_reference_id)
    WHERE status IN ('Open', 'Acknowledged', 'Escalated');

-- Table: observability.alert_events (Append-only audit trail)
CREATE TABLE IF NOT EXISTS observability.alert_events (
    alert_event_id UUID PRIMARY KEY,
    alert_id UUID NOT NULL REFERENCES observability.alerts(alert_id) ON DELETE CASCADE,
    event_type TEXT NOT NULL,
    actor_id UUID NULL REFERENCES identity.users(user_id) ON DELETE SET NULL,
    payload JSONB NOT NULL DEFAULT '{}'::jsonb,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_alert_events_alert_id ON observability.alert_events(alert_id, created_at ASC);
CREATE INDEX IF NOT EXISTS idx_alert_events_created_at ON observability.alert_events(created_at DESC);
