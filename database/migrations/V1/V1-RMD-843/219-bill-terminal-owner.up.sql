-- The terminal that owns the bill: it is claimed by the first terminal that opens or touches it. A terminal sees and handles
-- only its own bills; a user holding cash.session.override handles any. NULL means not claimed yet (rows older than this column).
ALTER TABLE billing.bills ADD COLUMN IF NOT EXISTS terminal_id UUID NULL;
