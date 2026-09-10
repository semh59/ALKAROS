-- V1-RMD-152: what a modifier actually consumes from the store room.
-- inventory.product_stock_mappings can only reference catalog.products, and
-- a modifier is not one of those rows — catalog.modifiers is its own table —
-- so extras like "ekstra peynir" had no way to be mapped at all and never
-- left the depot even though they were charged and cooked.
--
-- Same shape and same BOM arithmetic as the product mapping, deliberately:
-- one modifier can draw on several stock items, each with its own multiplier.
CREATE TABLE IF NOT EXISTS inventory.modifier_stock_mappings (
    modifier_id UUID NOT NULL,
    stock_item_id UUID NOT NULL REFERENCES inventory.stock_items(id) ON DELETE RESTRICT,
    quantity_multiplier NUMERIC(14, 4) NOT NULL CHECK (quantity_multiplier > 0),
    notes VARCHAR(255),
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT pk_modifier_stock_mappings PRIMARY KEY (modifier_id, stock_item_id)
);

CREATE INDEX IF NOT EXISTS idx_modifier_stock_mappings_stock_item
    ON inventory.modifier_stock_mappings (stock_item_id);
