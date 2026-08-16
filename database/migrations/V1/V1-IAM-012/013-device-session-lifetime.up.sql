ALTER TABLE identity.device_sessions
    ADD CONSTRAINT chk_device_sessions_lifetime
    CHECK (expires_at > created_at);
