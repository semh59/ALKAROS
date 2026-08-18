-- ============================================================================
-- Migration: 027-alerts.down.sql
-- Task: V1-ALT-001 (Implement Alert foundation)
-- ============================================================================

DROP TABLE IF EXISTS observability.alert_events CASCADE;
DROP TABLE IF EXISTS observability.alerts CASCADE;
