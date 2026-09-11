-- V1-WTR-013: an optional short note attached to a garson-masa hand-off
-- (orders.transfer-server, V1-RMD-111). Until now the endpoint moved every
-- open check to the target waiter with no way to say WHY ("table 5 is
-- waiting on dessert, table 8 complained") - the receiving waiter got the
-- tables with none of the context the departing one had.
--
-- Deliberately its own tiny table, not a field on orders.orders: a note is
-- about the HAND-OFF EVENT (one from-user, one to-user, one moment), not
-- about any single order, and it is meant to be read exactly once and then
-- gone - persisting it on every transferred order would leak it into
-- permanent order history with no natural place to mark it "seen".
--
-- Lives in the notifications schema alongside push_subscriptions
-- (V1-WTR-011) - same shape of thing: small, ephemeral, user-directed
-- operational metadata that is not part of any domain aggregate.
CREATE TABLE IF NOT EXISTS notifications.serving_handoff_notes (
    handoff_note_id UUID NOT NULL,
    from_user_id UUID NOT NULL REFERENCES identity.users(user_id) ON DELETE CASCADE,
    to_user_id UUID NOT NULL REFERENCES identity.users(user_id) ON DELETE CASCADE,
    note TEXT NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    -- Set the one time the receiving waiter's client "pops" it (read once,
    -- mark consumed, return it - atomic, no separate mark-as-read round
    -- trip). NULL means still pending.
    consumed_at TIMESTAMPTZ NULL,
    PRIMARY KEY (handoff_note_id),
    CONSTRAINT chk_serving_handoff_notes_note_length
        CHECK (char_length(note) BETWEEN 1 AND 200)
);

-- The one query this table exists to serve: "does this user have a pending
-- note", newest first (a waiter who received two hand-offs before opening
-- any table only needs the latest one - see the endpoint's own note).
CREATE INDEX IF NOT EXISTS idx_serving_handoff_notes_pending
    ON notifications.serving_handoff_notes (to_user_id, created_at DESC)
    WHERE consumed_at IS NULL;
