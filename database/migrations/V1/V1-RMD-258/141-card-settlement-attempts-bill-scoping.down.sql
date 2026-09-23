DROP INDEX IF EXISTS payments.uq_card_settlement_attempts_allocation;
DROP INDEX IF EXISTS payments.ix_card_settlement_attempts_bill;
ALTER TABLE payments.card_settlement_attempts DROP CONSTRAINT IF EXISTS fk_card_settlement_attempts_bill;
ALTER TABLE payments.card_settlement_attempts DROP COLUMN IF EXISTS bill_id;
