-- V1-RMD-112: billing.bill_adjustments had no idempotency protection at
-- all — a retried POST .../bills/{billId}/discount (network retry, or a
-- client resend after a timeout) appended a second discount row for the
-- exact same request. Unlike /comp and /void-sent, which happen to be
-- protected by their own ExpectedRowVersion optimistic-concurrency check,
-- ApplyBillDiscountRequestV1 carries no row version at all, and a fresh
-- Guid.NewGuid() was used for every adjustment id regardless of the
-- caller's own idempotency key. The partial unique index makes a
-- duplicate submission with the same key a no-op at the database level;
-- BillingSplitStore.ApplyDiscountAsync (already holding a FOR UPDATE lock
-- on the bill row) now checks for a matching key among the bill's
-- existing adjustments before creating a new one.
ALTER TABLE billing.bill_adjustments
    ADD COLUMN IF NOT EXISTS idempotency_key TEXT NULL;

CREATE UNIQUE INDEX IF NOT EXISTS ux_bill_adjustments_bill_idempotency
    ON billing.bill_adjustments (bill_id, idempotency_key)
    WHERE idempotency_key IS NOT NULL;
