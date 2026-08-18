-- ============================================================================
-- Migration: 028-health-checks.down.sql
-- Task: V1-OBS-001 (Implement observability correlation foundation)
-- ============================================================================

DROP TABLE IF EXISTS observability.health_checks CASCADE;
