-- ============================================================================
-- Migration: 030-reconciliation-cases.up.sql
-- Task: V1-REC-001 (Implement ReconciliationCase foundation)
-- Specification: PDF:I.16-I.20, II.2.21, II.3.15, II.5.12, II.6.11, III.23, V0-DOM-001, V0-DAT-002
-- ============================================================================

CREATE SCHEMA IF NOT EXISTS reconciliation;

-- Table: reconciliation.cases
CREATE TABLE IF NOT EXISTS reconciliation.cases (
    case_id UUID PRIMARY KEY,
    deduplication_key TEXT NOT NULL,
    case_type TEXT NOT NULL,
    source_a_ref TEXT NOT NULL,
    source_b_ref TEXT NOT NULL,
    discrepancy_amount NUMERIC(12,2) NOT NULL DEFAULT 0.00,
    severity TEXT NOT NULL,
    status TEXT NOT NULL,
    opened_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    resolved_at TIMESTAMPTZ NULL,
    row_version INT NOT NULL DEFAULT 1,
    details JSONB NULL,
    CONSTRAINT chk_rec_case_type CHECK (case_type IN ('PaymentMismatch', 'CashVariance', 'FiscalDiscrepancy', 'OnlineOrderMismatch', 'InventoryDiscrepancy')),
    CONSTRAINT chk_rec_case_severity CHECK (severity IN ('Low', 'Medium', 'High', 'Critical')),
    CONSTRAINT chk_rec_case_status CHECK (status IN ('Open', 'Investigating', 'Resolved', 'Dismissed', 'Escalated')),
    CONSTRAINT chk_rec_case_version CHECK (row_version >= 1)
);

CREATE UNIQUE INDEX IF NOT EXISTS uq_reconciliation_cases_active_dedup
    ON reconciliation.cases (deduplication_key)
    WHERE status IN ('Open', 'Investigating', 'Escalated');

CREATE INDEX IF NOT EXISTS idx_reconciliation_cases_status ON reconciliation.cases(status, opened_at DESC);
CREATE INDEX IF NOT EXISTS idx_reconciliation_cases_sources ON reconciliation.cases(source_a_ref, source_b_ref);

-- Table: reconciliation.case_actions (Append-only audit trail)
CREATE TABLE IF NOT EXISTS reconciliation.case_actions (
    action_id UUID PRIMARY KEY,
    case_id UUID NOT NULL REFERENCES reconciliation.cases(case_id) ON DELETE RESTRICT,
    action_type TEXT NOT NULL,
    performed_by UUID NOT NULL,
    performed_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    details JSONB NULL,
    CONSTRAINT chk_rec_action_type CHECK (action_type IN ('Created', 'Deduplicated', 'StatusChanged', 'NoteAdded', 'Resolved', 'Dismissed', 'Escalated'))
);

CREATE INDEX IF NOT EXISTS idx_rec_case_actions ON reconciliation.case_actions(case_id, performed_at ASC);
