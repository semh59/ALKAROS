-- A manager gives each terminal a short name (for example "Kasa 1") so screens can show it instead of the id prefix.
ALTER TABLE customer_display.terminals ADD COLUMN IF NOT EXISTS display_name TEXT NULL;
ALTER TABLE customer_display.terminals DROP CONSTRAINT IF EXISTS ck_terminals_display_name;
ALTER TABLE customer_display.terminals ADD CONSTRAINT ck_terminals_display_name
    CHECK (display_name IS NULL OR (display_name = btrim(display_name) AND length(display_name) BETWEEN 1 AND 40));
CREATE UNIQUE INDEX IF NOT EXISTS ux_customer_display_terminal_name
    ON customer_display.terminals (lower(display_name)) WHERE display_name IS NOT NULL;
