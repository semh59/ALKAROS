-- V1-RMD-151: an optional PIN a user can unlock their own device with.
-- Found while designing the kiosk lock (2026-09-10): a device session lives
-- 30 days and nothing ever asks who is holding the device, so a waiter's
-- phone left on a table stays open under that waiter's identity.
--
-- The PIN never authenticates on its own — POST /api/v1/auth/unlock requires
-- an already-valid device session and only proves the same person is still
-- there. pin_hash uses the same PBKDF2-HMAC-SHA256 encoding as password_hash.
--
-- The attempt counters are deliberately separate from the password ones: a
-- locked PIN must not lock the account (a waiter who forgot their PIN still
-- signs in with username and password), and PIN guesses must not consume the
-- password lockout budget.
ALTER TABLE identity.users
    ADD COLUMN IF NOT EXISTS pin_hash            VARCHAR(255) NULL,
    ADD COLUMN IF NOT EXISTS pin_failed_attempts INTEGER      NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS pin_locked_until    TIMESTAMPTZ  NULL;
