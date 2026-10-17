-- The printer family decides which Turkish code table command the ticket bytes start with. Null means the
-- ALKAROS_PRINTER_PROFILE environment default applies.
ALTER TABLE kitchen.printers ADD COLUMN IF NOT EXISTS profile VARCHAR(16) NULL;

ALTER TABLE kitchen.printers DROP CONSTRAINT IF EXISTS ck_printers_profile;
ALTER TABLE kitchen.printers ADD CONSTRAINT ck_printers_profile
    CHECK (profile IS NULL OR profile IN ('Epson', 'Bixolon', 'Star', 'Ascii'));
