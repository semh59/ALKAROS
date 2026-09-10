DROP INDEX IF EXISTS orders.ix_orders_pending_confirmation;

ALTER TABLE kitchen.kitchen_ticket_items
    DROP CONSTRAINT IF EXISTS fk_kitchen_ticket_items_product,
    DROP CONSTRAINT IF EXISTS fk_kitchen_ticket_items_order_item;

ALTER TABLE inventory.modifier_stock_mappings
    DROP CONSTRAINT IF EXISTS fk_modifier_stock_mappings_modifier;

ALTER TABLE inventory.product_stock_mappings
    DROP CONSTRAINT IF EXISTS fk_product_stock_mappings_product;

ALTER TABLE inventory.stock_movements
    DROP CONSTRAINT IF EXISTS ck_stock_movements_direction,
    DROP CONSTRAINT IF EXISTS ck_stock_movements_movement_type;

ALTER TABLE catalog.product_prices
    DROP CONSTRAINT IF EXISTS ck_product_prices_price_nonnegative;

ALTER TABLE orders.order_item_modifiers
    DROP CONSTRAINT IF EXISTS ck_order_item_modifiers_quantity_positive;

ALTER TABLE orders.order_items
    DROP CONSTRAINT IF EXISTS ck_order_items_tax_rate_nonnegative,
    DROP CONSTRAINT IF EXISTS ck_order_items_unit_price_nonnegative,
    DROP CONSTRAINT IF EXISTS ck_order_items_quantity_positive;
