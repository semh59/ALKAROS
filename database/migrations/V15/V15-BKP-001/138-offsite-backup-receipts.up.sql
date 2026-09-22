-- ============================================================================
-- Migration: 138-offsite-backup-receipts.up.sql
-- Task: V15-BKP-001 (Implement encrypted off-site backup)
-- Specification: PDF:I.38-I.44, PDF:II.2.23, PDF:III.25, V0-BKP-002
-- ============================================================================

CREATE SCHEMA IF NOT EXISTS operations;

-- Table: operations.offsite_backup_receipts
-- Append-only: the repository (PostgresOffsiteBackupReceiptStore) exposes no
-- UPDATE/DELETE method, and artifact_id is the primary key, so a repeat
-- upload of the same artifact id raises a unique-violation rather than
-- silently overwriting the receipt.
CREATE TABLE IF NOT EXISTS operations.offsite_backup_receipts (
    artifact_id TEXT PRIMARY KEY,
    data_class TEXT NOT NULL,
    plaintext_checksum_sha256 TEXT NOT NULL,
    encryption_key_name TEXT NOT NULL,
    encryption_key_version INT NOT NULL,
    plaintext_size_bytes BIGINT NOT NULL,
    target_location TEXT NOT NULL,
    uploaded_at TIMESTAMPTZ NOT NULL,
    CONSTRAINT chk_offsite_receipt_data_class CHECK (data_class IN ('Fiscal', 'OrdersInventory', 'Settings')),
    CONSTRAINT chk_offsite_receipt_key_version CHECK (encryption_key_version >= 1),
    CONSTRAINT chk_offsite_receipt_size CHECK (plaintext_size_bytes >= 0)
);

CREATE INDEX IF NOT EXISTS idx_offsite_backup_receipts_data_class_uploaded
    ON operations.offsite_backup_receipts(data_class, uploaded_at DESC);
