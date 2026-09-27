-- V1-RMD-359: ck_manual_card_decided_consistency (V1-RMD-283, migration 143) used a one-way boolean equality
-- ((status = 'Pending') = (decided_by IS NULL AND decided_at IS NULL)) - this only forced Pending to have both
-- decided_by/decided_at null; it never forced Approved/Rejected to have BOTH set together (a row missing just
-- one of the two - who decided, or when - still satisfied it, since the AND on the right only needs to
-- disagree with the left on TRUE/FALSE, not agree with it field by field). A four-eyes payment decision missing
-- either half is exactly the kind of accountability gap this table exists to prevent.
--
-- Migration 143's own .up.sql is not edited directly: MigrationExecutor.cs checksums every applied .up.sql on
-- both apply and rollback, so changing a historical forward file would break that checksum for any environment
-- that already ran it. This migration corrects the constraint in place instead.
ALTER TABLE payments.manual_card_confirmations
    DROP CONSTRAINT IF EXISTS ck_manual_card_decided_consistency;

ALTER TABLE payments.manual_card_confirmations
    ADD CONSTRAINT ck_manual_card_decided_consistency CHECK (
        (status = 'Pending' AND decided_by IS NULL AND decided_at IS NULL)
        OR (status <> 'Pending' AND decided_by IS NOT NULL AND decided_at IS NOT NULL));
