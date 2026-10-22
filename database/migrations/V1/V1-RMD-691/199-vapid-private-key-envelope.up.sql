-- The VAPID private key signs every push this deployment sends; anyone holding it can send as this store.
-- It is now kept as an encrypted envelope (the same master-key envelope the other stored credentials use).
-- A plaintext key left by an earlier version stays readable until the Host first reads it, then it is
-- encrypted and the plaintext column is cleared.
ALTER TABLE notifications.vapid_keys
    ALTER COLUMN private_key DROP NOT NULL;

ALTER TABLE notifications.vapid_keys
    ADD COLUMN IF NOT EXISTS private_key_envelope BYTEA;

ALTER TABLE notifications.vapid_keys
    ADD CONSTRAINT chk_vapid_keys_private_key_stored
    CHECK (private_key IS NOT NULL OR private_key_envelope IS NOT NULL);
