DROP TABLE IF EXISTS payments.payment_allocations;

ALTER TABLE billing.bills
    DROP CONSTRAINT IF EXISTS uq_bills_id_currency;

ALTER TABLE payments.payments
    DROP CONSTRAINT IF EXISTS uq_payments_id_currency;

ALTER TABLE payments.payments
    DROP CONSTRAINT IF EXISTS uq_payments_id_bill;
