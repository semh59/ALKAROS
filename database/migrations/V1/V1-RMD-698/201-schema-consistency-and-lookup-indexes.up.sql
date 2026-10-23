-- The screensaver row is bounded by the table itself, not only by the upload handler: allowed types and at most 20 MB.
-- A stored row that breaks either rule is removed first (the display falls back to its default idle card).
DELETE FROM customer_display.screensaver_images
WHERE content_type NOT IN ('image/png', 'image/jpeg', 'image/webp', 'video/mp4')
   OR octet_length(content) NOT BETWEEN 1 AND 20971520;

ALTER TABLE customer_display.screensaver_images
    ADD CONSTRAINT screensaver_images_content_type_check
        CHECK (content_type IN ('image/png', 'image/jpeg', 'image/webp', 'video/mp4')),
    ADD CONSTRAINT screensaver_images_size_check
        CHECK (octet_length(content) BETWEEN 1 AND 20971520);

-- Every other customer_display row_version is BIGINT.
ALTER TABLE customer_display.screensaver_images ALTER COLUMN row_version TYPE BIGINT;

-- A code or a session cannot end before it began; a row that already does is unusable and is removed first.
DELETE FROM customer_display.pairing_requests WHERE expires_at <= created_at;
DELETE FROM customer_display.display_sessions WHERE expires_at <= created_at;

ALTER TABLE customer_display.pairing_requests
    ADD CONSTRAINT pairing_requests_expiry_check CHECK (expires_at > created_at);
ALTER TABLE customer_display.display_sessions
    ADD CONSTRAINT display_sessions_expiry_check CHECK (expires_at > created_at);

-- Deleting or updating an order has to find the terminals that point at it without scanning the table.
CREATE INDEX IF NOT EXISTS ix_terminals_active_order
    ON customer_display.terminals (active_order_id)
    WHERE active_order_id IS NOT NULL;

-- The receipt asks, per payment, whether a ledger row exists for it; the existing indexes both start with customer_id.
CREATE INDEX IF NOT EXISTS ix_account_transactions_source_reference
    ON customer_account.account_transactions (source_reference_id, source_reference_type);
