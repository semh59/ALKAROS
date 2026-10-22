-- Reverse of 199. A key that exists only as an envelope cannot be turned back into plaintext in SQL, so its
-- row is removed: the Host creates a new key pair on next use and devices subscribe again.
DELETE FROM notifications.vapid_keys WHERE private_key IS NULL;

ALTER TABLE notifications.vapid_keys
    DROP CONSTRAINT IF EXISTS chk_vapid_keys_private_key_stored;

ALTER TABLE notifications.vapid_keys
    DROP COLUMN IF EXISTS private_key_envelope;

ALTER TABLE notifications.vapid_keys
    ALTER COLUMN private_key SET NOT NULL;
