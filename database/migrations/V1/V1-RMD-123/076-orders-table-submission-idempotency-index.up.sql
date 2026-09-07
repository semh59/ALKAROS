-- V1-RMD-123: found by a fresh independent audit (2026-09-07) — a
-- table-draft retry after the order had already been fully submitted
-- (the response was lost to a dropped connection, a common offline-queue
-- scenario) created a whole second order for the same table instead of
-- replaying the first: GetActiveOrderByTableIdInternalAsync only ever
-- looked for a still-Draft order, so once the original moved to
-- Submitted the retry found nothing and started a brand-new one — a real
-- double-kitchen-dispatch risk. Both production clients (waiter-app.js,
-- cashier-app.js) already generate and resend a stable client-side id for
-- exactly this retry scenario, but the server never read or stored it.
-- orders.orders.source_reference_id (already present, unused for the
-- Waiter/Cashier "table-draft" source) now carries that client-generated
-- submission id; the partial unique index makes two concurrent identical
-- retries for the same table resolve to the same order at the database
-- level, not just in application code.
CREATE UNIQUE INDEX IF NOT EXISTS ux_orders_table_submission
    ON orders.orders (table_id, source_reference_id)
    WHERE source_reference_id IS NOT NULL;
