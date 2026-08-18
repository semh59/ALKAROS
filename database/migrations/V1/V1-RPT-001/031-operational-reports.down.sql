-- ============================================================================
-- Migration: 031-operational-reports.down.sql
-- Task: V1-RPT-001 (Implement V1 operational reports)
-- ============================================================================

DROP TABLE IF EXISTS reporting.print_error_summaries CASCADE;
DROP TABLE IF EXISTS reporting.waiter_performance_summaries CASCADE;
DROP TABLE IF EXISTS reporting.daily_business_days CASCADE;
