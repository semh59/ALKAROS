-- ============================================================================
-- Migration: 139-restore-attempts.up.sql
-- Task: V15-BKP-002 (Implement isolated restore verification)
-- Specification: PDF:I.38-I.44, PDF:II.2.23, PDF:III.25, V0-BKP-002
-- ============================================================================

CREATE SCHEMA IF NOT EXISTS operations;

-- Table: operations.restore_attempts
-- Append-only: the repository (PostgresRestoreAttemptStore) exposes no
-- UPDATE/DELETE method. One row per restore drill, success or failure, so
-- RTO trend and drill history survive the process that ran it.
CREATE TABLE IF NOT EXISTS operations.restore_attempts (
    attempt_id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    artifact_id TEXT NOT NULL,
    data_class TEXT NOT NULL,
    started_at TIMESTAMPTZ NOT NULL,
    duration_ms BIGINT NOT NULL,
    succeeded BOOLEAN NOT NULL,
    within_rto_target BOOLEAN NOT NULL,
    integrity_checks_passed INT NOT NULL,
    integrity_checks_total INT NOT NULL,
    failure_reason TEXT,
    CONSTRAINT chk_restore_attempt_data_class CHECK (data_class IN ('Fiscal', 'OrdersInventory', 'Settings')),
    CONSTRAINT chk_restore_attempt_duration CHECK (duration_ms >= 0),
    CONSTRAINT chk_restore_attempt_checks CHECK (integrity_checks_passed >= 0 AND integrity_checks_passed <= integrity_checks_total),
    CONSTRAINT chk_restore_attempt_failure_reason CHECK (succeeded OR failure_reason IS NOT NULL)
);

CREATE INDEX IF NOT EXISTS idx_restore_attempts_data_class_started
    ON operations.restore_attempts(data_class, started_at DESC);
