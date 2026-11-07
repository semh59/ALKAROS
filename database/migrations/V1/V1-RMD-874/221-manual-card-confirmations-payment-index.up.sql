CREATE INDEX IF NOT EXISTS ix_manual_card_confirmations_payment_status
    ON payments.manual_card_confirmations (payment_id, status);
