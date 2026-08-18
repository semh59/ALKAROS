-- ============================================================================
-- Migration: 030-reconciliation-cases.down.sql
-- Task: V1-REC-001 (Implement ReconciliationCase foundation)
-- ============================================================================

DROP TABLE IF EXISTS reconciliation.case_actions CASCADE;
DROP TABLE IF EXISTS reconciliation.cases CASCADE;
