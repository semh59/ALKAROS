-- Migration 137: Sensitive-payload retention subjects (V15-SEC-003)
-- One row per sensitive envelope under generic retention-execution control
-- (V0-CMP-003's disposal matrix categories). The row is the audit trail
-- itself for Anonymize-class disposal: envelope_bytes is overwritten with a
-- non-decryptable sentinel in place, id/category/timestamps survive. A
-- Delete-class disposal instead marks the row for the deletion queue
-- (disposed_at/disposal_action set, envelope untouched) until the queue
-- processor hard-deletes it.
CREATE SCHEMA IF NOT EXISTS security;

CREATE TABLE IF NOT EXISTS security.retention_subjects (
    id              UUID        NOT NULL,
    data_category   TEXT        NOT NULL,
    envelope_bytes  BYTEA       NOT NULL,
    created_at      TIMESTAMPTZ NOT NULL,
    legal_hold      BOOLEAN     NOT NULL DEFAULT FALSE,
    disposed_at     TIMESTAMPTZ NULL,
    disposal_action TEXT        NULL,
    row_version     INTEGER     NOT NULL DEFAULT 1 CHECK (row_version > 0),
    PRIMARY KEY (id),
    CONSTRAINT retention_subjects_disposal_action_check
        CHECK (disposal_action IS NULL OR disposal_action IN ('Anonymize', 'Delete')),
    CONSTRAINT retention_subjects_disposal_consistency_check
        CHECK ((disposed_at IS NULL) = (disposal_action IS NULL)),
    -- Mirrors DataCategory's 9 enum values (ALKAROS.Security.DataProtectionRetention.DataCategory) —
    -- update both if a category is ever added.
    CONSTRAINT retention_subjects_data_category_check
        CHECK (data_category IN (
            'CustomerPii', 'UserCredentials', 'OrderNotes', 'ProviderPayloads',
            'AuditLogs', 'FiscalData', 'InvoiceData', 'SupplierData', 'DeviceData'
        ))
);

-- Predicate must match PostgresRetentionSubjectStore.GetPendingAsync's actual
-- WHERE clause (disposed_at IS NULL only - legal_hold is filtered in memory
-- by RetentionExecutionService, not in SQL) or the planner cannot use this
-- index at all.
CREATE INDEX IF NOT EXISTS ix_retention_subjects_pending
    ON security.retention_subjects (data_category, created_at)
    WHERE disposed_at IS NULL;

CREATE INDEX IF NOT EXISTS ix_retention_subjects_delete_queue
    ON security.retention_subjects (id)
    WHERE disposal_action = 'Delete' AND disposed_at IS NOT NULL;
