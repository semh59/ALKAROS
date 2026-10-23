ALTER TABLE customer_display.terminals
    DROP CONSTRAINT ck_customer_display_terminal_key_hash,
    DROP COLUMN terminal_key_hash;
