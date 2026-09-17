DROP TABLE IF EXISTS payments.payment_allocations;

ALTER TABLE billing.bills
    DROP CONSTRAINT uq_bills_id_currency;

ALTER TABLE payments.payments
    DROP CONSTRAINT uq_payments_id_currency;

ALTER TABLE payments.payments
    DROP CONSTRAINT uq_payments_id_bill;
