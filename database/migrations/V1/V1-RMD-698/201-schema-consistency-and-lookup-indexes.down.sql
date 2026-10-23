DROP INDEX IF EXISTS customer_account.ix_account_transactions_source_reference;
DROP INDEX IF EXISTS customer_display.ix_terminals_active_order;

ALTER TABLE customer_display.display_sessions DROP CONSTRAINT IF EXISTS display_sessions_expiry_check;
ALTER TABLE customer_display.pairing_requests DROP CONSTRAINT IF EXISTS pairing_requests_expiry_check;

ALTER TABLE customer_display.screensaver_images ALTER COLUMN row_version TYPE INTEGER;
ALTER TABLE customer_display.screensaver_images
    DROP CONSTRAINT IF EXISTS screensaver_images_size_check,
    DROP CONSTRAINT IF EXISTS screensaver_images_content_type_check;
