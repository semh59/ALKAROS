ALTER TABLE kitchen.printers DROP CONSTRAINT IF EXISTS ck_printers_profile;
ALTER TABLE kitchen.printers DROP COLUMN IF EXISTS profile;
