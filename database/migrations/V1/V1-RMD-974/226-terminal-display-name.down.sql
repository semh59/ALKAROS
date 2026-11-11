DROP INDEX IF EXISTS customer_display.ux_customer_display_terminal_name;
ALTER TABLE customer_display.terminals DROP CONSTRAINT IF EXISTS ck_terminals_display_name;
ALTER TABLE customer_display.terminals DROP COLUMN IF EXISTS display_name;
