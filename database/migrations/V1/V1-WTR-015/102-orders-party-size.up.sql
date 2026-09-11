-- V1-WTR-015: party size (cover count) — a "Katman B" gap closed from the
-- garson comparison doc (2026-09-11 ideation): every rival POS tracks how
-- many guests are at a table, ALKAROS tracked none. Nullable and set once,
-- at the moment a table's first round is drafted (Order creation) — the
-- waiter has just greeted the table and knows the count then; correcting
-- it mid-meal is a deliberate follow-on, not this migration's job.
ALTER TABLE orders.orders
    ADD COLUMN IF NOT EXISTS party_size INTEGER NULL;

ALTER TABLE orders.orders
    ADD CONSTRAINT chk_orders_party_size CHECK (party_size IS NULL OR party_size BETWEEN 1 AND 50);
