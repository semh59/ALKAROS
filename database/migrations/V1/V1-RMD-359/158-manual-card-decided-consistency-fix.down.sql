-- V1-RMD-359 rollback: restores migration 143's original constraint text exactly, including its bug - a
-- rollback of this fix's own deployment, not a way to re-introduce the gap into a healthy system on purpose.
ALTER TABLE payments.manual_card_confirmations
    DROP CONSTRAINT IF EXISTS ck_manual_card_decided_consistency;

ALTER TABLE payments.manual_card_confirmations
    ADD CONSTRAINT ck_manual_card_decided_consistency CHECK ((status = 'Pending') = (decided_by IS NULL AND decided_at IS NULL));
