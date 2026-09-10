ALTER TABLE identity.users
    DROP COLUMN IF EXISTS pin_locked_until,
    DROP COLUMN IF EXISTS pin_failed_attempts,
    DROP COLUMN IF EXISTS pin_hash;
