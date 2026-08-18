-- ============================================================================
-- Migration: 029-backup-health.up.sql
-- Task: V1-OPS-002 (Implement local backup and health foundation)
-- Specification: PDF:I.16-I.20, II.2.23, III.25, V0-BKP-001, V0-DAT-002
-- ============================================================================

CREATE SCHEMA IF NOT EXISTS operations;

-- Table: operations.backups
CREATE TABLE IF NOT EXISTS operations.backups (
    backup_id UUID PRIMARY KEY,
    backup_type TEXT NOT NULL,
    file_path TEXT NOT NULL,
    file_size_bytes BIGINT NOT NULL,
    checksum_sha256 TEXT NOT NULL,
    status TEXT NOT NULL,
    error_message TEXT NULL,
    started_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    completed_at TIMESTAMPTZ NULL,
    retention_days INT NOT NULL DEFAULT 30,
    metadata JSONB NULL,
    CONSTRAINT chk_backup_type CHECK (backup_type IN ('Full', 'Incremental', 'SchemaOnly')),
    CONSTRAINT chk_backup_status CHECK (status IN ('InProgress', 'Completed', 'Failed'))
);

CREATE INDEX IF NOT EXISTS idx_backups_status_started ON operations.backups(status, started_at DESC);
CREATE INDEX IF NOT EXISTS idx_backups_completed ON operations.backups(completed_at DESC) WHERE status = 'Completed';

-- Table: operations.system_health_snapshots
CREATE TABLE IF NOT EXISTS operations.system_health_snapshots (
    snapshot_id UUID PRIMARY KEY,
    database_status TEXT NOT NULL,
    disk_status TEXT NOT NULL,
    last_backup_status TEXT NOT NULL,
    free_disk_bytes BIGINT NOT NULL,
    database_size_bytes BIGINT NOT NULL,
    captured_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    details JSONB NULL,
    CONSTRAINT chk_snap_db_status CHECK (database_status IN ('Healthy', 'Degraded', 'Unhealthy')),
    CONSTRAINT chk_snap_disk_status CHECK (disk_status IN ('Healthy', 'Degraded', 'Unhealthy')),
    CONSTRAINT chk_snap_backup_status CHECK (last_backup_status IN ('Healthy', 'Degraded', 'Unhealthy'))
);

CREATE INDEX IF NOT EXISTS idx_system_health_captured ON operations.system_health_snapshots(captured_at DESC);
