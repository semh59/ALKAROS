-- ============================================================================
-- Migration: 028-health-checks.up.sql
-- Task: V1-OBS-001 (Implement observability correlation foundation)
-- Specification: PDF:III.28 (III.28.1 health_checks), II.2.25, II.5.13, V0-DAT-002
-- ============================================================================

CREATE SCHEMA IF NOT EXISTS observability;

-- Table: observability.health_checks
CREATE TABLE IF NOT EXISTS observability.health_checks (
    health_check_id UUID PRIMARY KEY,
    check_type TEXT NOT NULL,
    target TEXT NOT NULL,
    status TEXT NOT NULL,
    checked_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    retention_policy_id TEXT NOT NULL,
    details JSONB NULL,
    CONSTRAINT chk_health_checks_status CHECK (status IN ('Healthy', 'Degraded', 'Unhealthy'))
);

CREATE INDEX IF NOT EXISTS idx_health_checks_target_checked ON observability.health_checks(target, checked_at DESC);
CREATE INDEX IF NOT EXISTS idx_health_checks_status ON observability.health_checks(status) WHERE status != 'Healthy';
CREATE INDEX IF NOT EXISTS idx_health_checks_retention ON observability.health_checks(retention_policy_id, checked_at ASC);
