-- V1-RMD-137: an age-restricted item's kitchen ticket line must carry that
-- flag through to whoever prepares/serves it, independent of catalog.products
-- (which can change after the ticket is printed) — this is a point-in-time
-- snapshot, same rationale as product_name_snapshot on the same table.
ALTER TABLE kitchen.kitchen_ticket_items
    ADD COLUMN IF NOT EXISTS is_age_restricted BOOLEAN NOT NULL DEFAULT FALSE;
