-- V12-ONL-006: an online order is identified by the platform it came from together with that platform's own order
-- number. Two platforms may use the same number; one platform can never have two local orders for one number. The
-- link lives in the online ordering schema so orders.orders (and every fixture that loads it) stays unchanged.
CREATE TABLE IF NOT EXISTS online_ordering.online_orders (
    order_id          UUID        PRIMARY KEY,
    provider          TEXT        NOT NULL CHECK (provider ~ '^[a-z][a-z0-9-]{1,31}$'),
    external_order_id TEXT        NOT NULL CHECK (length(external_order_id) BETWEEN 1 AND 64),
    created_at        TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT uq_online_orders_provider_external_id UNIQUE (provider, external_order_id)
);

-- Every online order so far came from Yemeksepeti.
INSERT INTO online_ordering.online_orders (order_id, provider, external_order_id)
SELECT order_id, 'yemeksepeti', source_external_id
FROM orders.orders
WHERE source = 'Online' AND source_external_id IS NOT NULL
ON CONFLICT DO NOTHING;

-- Replaced by the per-platform uniqueness above (the V12-RMD-004 index ignored the platform).
DROP INDEX IF EXISTS orders.uq_orders_online_source_external_id;
