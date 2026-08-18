-- ============================================================================
-- Migration: 029-backup-health.down.sql
-- Task: V1-OPS-002 (Implement local backup and health foundation)
-- ============================================================================

DROP TABLE IF EXISTS operations.system_health_snapshots CASCADE;
DROP TABLE IF EXISTS operations.backups CASCADE;
