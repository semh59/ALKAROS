-- A terminal id is only a name; the browser's secret key proves it is the same browser. Only the hash is stored.
-- NULL = not claimed yet (a terminal that existed before this migration adopts the key of its next login).
ALTER TABLE customer_display.terminals
    ADD COLUMN terminal_key_hash TEXT NULL,
    ADD CONSTRAINT ck_customer_display_terminal_key_hash
        CHECK (terminal_key_hash IS NULL OR terminal_key_hash ~ '^[0-9A-F]{64}$');
