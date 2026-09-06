DROP INDEX IF EXISTS billing.ux_bill_adjustments_bill_idempotency;

ALTER TABLE billing.bill_adjustments
    DROP COLUMN IF EXISTS idempotency_key;
