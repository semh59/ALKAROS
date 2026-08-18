-- ============================================================================
-- Migration: 026-typed-settings.down.sql
-- Task: V1-SET-001 (Implement typed module-owned settings)
-- ============================================================================

DROP TABLE IF EXISTS settings.setting_history CASCADE;
DROP TABLE IF EXISTS settings.settings CASCADE;
