-- V1-WTR-011: what a device needs so the kitchen can reach it while the app
-- is closed.
--
-- Until now every waiter notification travelled over SignalR only
-- (V1-WTR-009 "item is ready", V1-RMD-149 "a guest ordered"), which means it
-- travelled only while the app was open and connected. A waiter who put the
-- phone in a pocket got nothing at all, and the notification table in
-- foundations.md 5.3 had a Push row (app closed or backgrounded) with no
-- implementation behind it.
--
-- Its own schema on purpose: the VAPID private key must never be reachable
-- through the generic settings surface, so it does not live in
-- settings.settings next to display preferences.
CREATE SCHEMA IF NOT EXISTS notifications;

-- One row per browser push subscription. The endpoint is the push service's
-- own URL for this device and is globally unique, so re-subscribing the same
-- device updates the row instead of piling up duplicates (a browser reissues
-- a subscription whenever its keys rotate).
CREATE TABLE IF NOT EXISTS notifications.push_subscriptions (
    subscription_id UUID NOT NULL,
    endpoint TEXT NOT NULL,
    -- RFC 8291 §3: the client's P-256 public key (uncompressed point) and its
    -- 16-byte auth secret, both base64url without padding, exactly as the
    -- browser's PushSubscription reports them.
    p256dh TEXT NOT NULL,
    auth TEXT NOT NULL,
    user_id UUID NOT NULL REFERENCES identity.users(user_id) ON DELETE CASCADE,
    terminal_id UUID NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    last_success_at TIMESTAMPTZ NULL,
    -- A push service answers 404/410 once a subscription is dead; that
    -- deletes the row outright (RFC 8030 §7.3). This counter only tracks the
    -- softer failures so a permanently broken endpoint can be spotted.
    failure_count INTEGER NOT NULL DEFAULT 0,
    PRIMARY KEY (subscription_id),
    CONSTRAINT uq_push_subscriptions_endpoint UNIQUE (endpoint),
    CONSTRAINT chk_push_subscriptions_failure_count CHECK (failure_count >= 0)
);

CREATE INDEX IF NOT EXISTS idx_push_subscriptions_user
    ON notifications.push_subscriptions (user_id);
CREATE INDEX IF NOT EXISTS idx_push_subscriptions_terminal
    ON notifications.push_subscriptions (terminal_id);

-- The server's VAPID identity (RFC 8292). One deployment, one key pair: if it
-- is regenerated every existing subscription stops accepting pushes, so the
-- pair is created once and then read, never rewritten. The single_row column
-- is the guard that keeps it that way.
CREATE TABLE IF NOT EXISTS notifications.vapid_keys (
    single_row BOOLEAN NOT NULL DEFAULT TRUE,
    public_key TEXT NOT NULL,
    private_key TEXT NOT NULL,
    subject TEXT NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    PRIMARY KEY (single_row),
    CONSTRAINT chk_vapid_keys_single_row CHECK (single_row)
);
