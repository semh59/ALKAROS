-- ============================================================================
-- Migration: 031-operational-reports.up.sql
-- Task: V1-RPT-001 (Implement V1 operational reports)
-- Specification: PDF:II.2.20, II.10, III.31, V0-DOM-008
-- ============================================================================

CREATE SCHEMA IF NOT EXISTS reporting;

-- Table: reporting.daily_business_days
CREATE TABLE IF NOT EXISTS reporting.daily_business_days (
    business_day_id UUID PRIMARY KEY,
    business_date DATE NOT NULL UNIQUE,
    opened_at TIMESTAMPTZ NOT NULL,
    closed_at TIMESTAMPTZ NULL,
    status TEXT NOT NULL,
    total_revenue NUMERIC(12,2) NOT NULL DEFAULT 0.00,
    total_orders_count INT NOT NULL DEFAULT 0,
    total_cancelled_items_count INT NOT NULL DEFAULT 0,
    total_print_failures_count INT NOT NULL DEFAULT 0,
    CONSTRAINT chk_biz_status CHECK (status IN ('Open', 'Closed'))
);

CREATE INDEX IF NOT EXISTS idx_reporting_biz_date ON reporting.daily_business_days(business_date DESC);

-- Table: reporting.waiter_performance_summaries
CREATE TABLE IF NOT EXISTS reporting.waiter_performance_summaries (
    summary_id UUID PRIMARY KEY,
    business_date DATE NOT NULL,
    waiter_user_id UUID NOT NULL,
    orders_served_count INT NOT NULL DEFAULT 0,
    total_sales_amount NUMERIC(12,2) NOT NULL DEFAULT 0.00,
    cancellations_count INT NOT NULL DEFAULT 0,
    discounts_applied_amount NUMERIC(12,2) NOT NULL DEFAULT 0.00,
    captured_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_waiter_perf_date ON reporting.waiter_performance_summaries(business_date, waiter_user_id);

-- Table: reporting.print_error_summaries
CREATE TABLE IF NOT EXISTS reporting.print_error_summaries (
    error_summary_id UUID PRIMARY KEY,
    business_date DATE NOT NULL,
    station_name TEXT NOT NULL,
    total_print_jobs INT NOT NULL DEFAULT 0,
    failed_print_jobs INT NOT NULL DEFAULT 0,
    recovered_print_jobs INT NOT NULL DEFAULT 0,
    captured_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_print_err_summary_date ON reporting.print_error_summaries(business_date, station_name);
