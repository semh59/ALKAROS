-- V1-RMD-156: closes the database block of the 2026-09-10 five-agent Garson
-- audit (docs/engineering/garson-audit-2026-09-10.md). Every gap below was
-- proved by actually inserting the bad row on a scratch database, not just
-- read from the schema.

-- Critical: orders.order_items enforced amount relationships (discount <=
-- gross, gross = net + tax, V1-RMD-002/036) but never the sign of quantity,
-- unit_price or tax_rate themselves. A line with quantity=-3, unit_price=-10
-- inserted cleanly with gross_amount=30 — a normal-looking charge whose
-- stored quantity and price are both negative, which corrupts anything that
-- trusts them directly (OrderStockConsumptionService's
-- item.Quantity * mapping.QuantityMultiplier has no sign check either).
-- Quantity must also be strictly positive: a zero-quantity line inserted
-- too, and OrderItem's own domain constructor already requires quantity > 0
-- (OrderItem.cs) — the column was the only place that rule was missing.
ALTER TABLE orders.order_items
    ADD CONSTRAINT ck_order_items_quantity_positive CHECK (quantity > 0),
    ADD CONSTRAINT ck_order_items_unit_price_nonnegative CHECK (unit_price >= 0),
    ADD CONSTRAINT ck_order_items_tax_rate_nonnegative CHECK (tax_rate >= 0);

-- Same gap one level down: a modifier line with quantity=-7 inserted too.
ALTER TABLE orders.order_item_modifiers
    ADD CONSTRAINT ck_order_item_modifiers_quantity_positive CHECK (quantity > 0);

-- catalog.product_prices.price had no sign check at all, unlike
-- catalog.products.current_price (022's chk_products_current_price_nonnegative).
-- -15.00 inserted and read back as a live price.
ALTER TABLE catalog.product_prices
    ADD CONSTRAINT ck_product_prices_price_nonnegative CHECK (price >= 0);

-- High: inventory.stock_movements.movement_type/direction are free VARCHAR
-- with zero CHECK anywhere in the migration history — ('Banana', 'Sideways')
-- inserted without error. This is worse than an ordinary typo risk because
-- the table is immutable by design (trg_stock_movements_immutable): a bad
-- value written once can only be reversed by another movement, never
-- corrected in place. Locked to the exact sets MovementType and
-- MovementDirection define in C#
-- (src/Modules/Inventory/MovementLedger/{StockMovementType,MovementDirection}.cs).
ALTER TABLE inventory.stock_movements
    ADD CONSTRAINT ck_stock_movements_movement_type CHECK (movement_type IN (
        'PurchaseReceipt', 'ProductionOutput', 'Consumption', 'Reservation',
        'Release', 'Waste', 'Adjustment', 'Return', 'Reversal'
    )),
    ADD CONSTRAINT ck_stock_movements_direction CHECK (direction IN (
        'In', 'Out', 'Reserve', 'Release'
    ));

-- High: product_stock_mappings.product_id and modifier_stock_mappings
-- .modifier_id carried no foreign key at all, unlike stock_item_id in the
-- same rows (ON DELETE RESTRICT). A mapping row was inserted pointing at a
-- random, nonexistent product_id/modifier_id and nothing caught it — the
-- exact stealth failure V1-RMD-152's own comment describes fixing for
-- modifiers: the mapping never matches at Accept time and stock silently
-- stops being consumed for that product, with no error anywhere.
ALTER TABLE inventory.product_stock_mappings
    ADD CONSTRAINT fk_product_stock_mappings_product
    FOREIGN KEY (product_id) REFERENCES catalog.products (product_id) ON DELETE CASCADE;

ALTER TABLE inventory.modifier_stock_mappings
    ADD CONSTRAINT fk_modifier_stock_mappings_modifier
    FOREIGN KEY (modifier_id) REFERENCES catalog.modifiers (modifier_id) ON DELETE CASCADE;

-- High: kitchen_ticket_items.order_item_id/.product_id were bare UUIDs —
-- ticket_id was FK'd ON DELETE CASCADE to kitchen_tickets, but a fabricated
-- order_item_id/product_id inserted cleanly. billing.bill_items FKs
-- order_item_id in the same schema generation; kitchen_ticket_items was the
-- one table left without it, so a kitchen ticket line could reference an
-- order item that never existed or has since been deleted, breaking any
-- join back from kitchen to the order/product it is supposed to represent.
-- RESTRICT rather than CASCADE: a ticket item outliving its order item is a
-- data-integrity bug to surface, not something to quietly delete.
ALTER TABLE kitchen.kitchen_ticket_items
    ADD CONSTRAINT fk_kitchen_ticket_items_order_item
    FOREIGN KEY (order_item_id) REFERENCES orders.order_items (order_item_id) ON DELETE RESTRICT,
    ADD CONSTRAINT fk_kitchen_ticket_items_product
    FOREIGN KEY (product_id) REFERENCES catalog.products (product_id) ON DELETE RESTRICT;

-- High: GetPendingOrdersAsync (the surface a waiter's guest-order banner
-- polls as a fallback to the live announcement) filters
-- orders.orders WHERE status = 'PendingConfirmation' with no supporting
-- index — two full sequential scans measured at 72ms on a 60k/180k
-- synthetic dataset, and PendingConfirmation orders are always a small
-- fraction of the table. Same partial-index shape V1-RMD-089's
-- ix_orders_table_open already uses for the same reason.
CREATE INDEX IF NOT EXISTS ix_orders_pending_confirmation
    ON orders.orders (created_at)
    WHERE status = 'PendingConfirmation';
