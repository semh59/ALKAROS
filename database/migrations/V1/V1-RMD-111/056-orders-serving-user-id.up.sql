-- V1-RMD-111: closes the own-check authorization gap (docs/domain/
-- authorization-model.md §3, resolved decision #1) — the guard existed in
-- AuthorizationGrantService but never fired because no order recorded who
-- created it. serving_user_id is set once at creation and changed only via
-- an explicit hand-off (Order.ReassignServer, orders.transfer-server[-any]).
-- No FK to identity.users, deliberately: same precedent as this table's own
-- order_status_history.changed_by (011) — Orders owns its schema and does
-- not hard-reference Identity's tables (V0-ARC-001 module boundary); a
-- narrower test fixture (e.g. tests/Modules/Orders/OrderAggregate) can load
-- orders.orders without ever loading identity.users.
ALTER TABLE orders.orders
    ADD COLUMN IF NOT EXISTS serving_user_id UUID NULL;

CREATE INDEX IF NOT EXISTS ix_orders_serving_user ON orders.orders (serving_user_id)
    WHERE serving_user_id IS NOT NULL;
