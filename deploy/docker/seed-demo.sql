-- Local demo data: a small floor plan and menu so the WaiterPwa / PosTerminal
-- show something on a fresh stack. Idempotent (fixed ids, ON CONFLICT DO
-- NOTHING). NOT for production - the go-live path seeds the real catalog and
-- floor through the manager UI.
--
--   docker compose -f compose.yaml -f compose.dev.yaml --profile seed run --rm seed
-- or:
--   docker compose exec -T postgres psql -U alkaros -d alkaros < deploy/docker/seed-demo.sql

BEGIN;

INSERT INTO table_mgmt.zones (zone_id, code, name, sort_order, active) VALUES
  ('a0000000-0000-0000-0000-000000000001', 'SALON', 'Salon', 1, true),
  ('a0000000-0000-0000-0000-000000000002', 'TERAS', 'Teras', 2, true)
ON CONFLICT (zone_id) DO NOTHING;

INSERT INTO table_mgmt.tables (table_id, zone_id, table_number, capacity, active, current_status) VALUES
  ('b0000000-0000-0000-0000-000000000001', 'a0000000-0000-0000-0000-000000000001', 'S1', 4, true, 'Available'),
  ('b0000000-0000-0000-0000-000000000002', 'a0000000-0000-0000-0000-000000000001', 'S2', 4, true, 'Available'),
  ('b0000000-0000-0000-0000-000000000003', 'a0000000-0000-0000-0000-000000000001', 'S3', 2, true, 'Available'),
  ('b0000000-0000-0000-0000-000000000004', 'a0000000-0000-0000-0000-000000000002', 'T1', 6, true, 'Available'),
  ('b0000000-0000-0000-0000-000000000005', 'a0000000-0000-0000-0000-000000000002', 'T2', 4, true, 'Available')
ON CONFLICT (table_id) DO NOTHING;

INSERT INTO catalog.categories (category_id, parent_category_id, code, name, sort_order, active) VALUES
  ('c0000000-0000-0000-0000-000000000001', NULL, 'YIYECEK', 'Yiyecek', 1, true),
  ('c0000000-0000-0000-0000-000000000002', NULL, 'ICECEK', 'Icecek', 2, true)
ON CONFLICT (category_id) DO NOTHING;

INSERT INTO catalog.products
  (product_id, sku, name, description, category_id, tax_profile_id, product_type, stock_mode, active, display_order, current_price) VALUES
  ('d0000000-0000-0000-0000-000000000001', 'FOOD-001', 'Izgara Kofte',      NULL, 'c0000000-0000-0000-0000-000000000001', NULL, 1, 1, true, 1, 220.00),
  ('d0000000-0000-0000-0000-000000000002', 'FOOD-002', 'Adana Kebap',       NULL, 'c0000000-0000-0000-0000-000000000001', NULL, 1, 1, true, 2, 260.00),
  ('d0000000-0000-0000-0000-000000000003', 'FOOD-003', 'Mercimek Corbasi',  NULL, 'c0000000-0000-0000-0000-000000000001', NULL, 1, 1, true, 3, 90.00),
  ('d0000000-0000-0000-0000-000000000004', 'DRNK-001', 'Ayran',             NULL, 'c0000000-0000-0000-0000-000000000002', NULL, 1, 1, true, 1, 40.00),
  ('d0000000-0000-0000-0000-000000000005', 'DRNK-002', 'Salgam',            NULL, 'c0000000-0000-0000-0000-000000000002', NULL, 1, 1, true, 2, 45.00),
  ('d0000000-0000-0000-0000-000000000006', 'DRNK-003', 'Cay',               NULL, 'c0000000-0000-0000-0000-000000000002', NULL, 1, 1, true, 3, 25.00)
ON CONFLICT (product_id) DO NOTHING;

-- Stock: an order is refused for a product with no stock mapping, so every demo
-- product maps 1:1 to a portion stock item. The opening quantity is a ledger
-- movement plus the balance row it sums to, so a rebuild of the balances from
-- the ledger keeps it. Re-running never resets stock that has been sold.
INSERT INTO inventory.stock_locations (id, code, name, location_type) VALUES
  ('f0000000-0000-0000-0000-000000000001', 'DEMO-MUTFAK', 'Demo Mutfak', 'Kitchen')
ON CONFLICT DO NOTHING;

INSERT INTO inventory.stock_items (id, code, name, item_type, tracking_unit_code, default_location_id) VALUES
  ('f1000000-0000-0000-0000-000000000001', 'DEMO-FOOD-001', 'Izgara Kofte',     'Portion', 'adet', 'f0000000-0000-0000-0000-000000000001'),
  ('f1000000-0000-0000-0000-000000000002', 'DEMO-FOOD-002', 'Adana Kebap',      'Portion', 'adet', 'f0000000-0000-0000-0000-000000000001'),
  ('f1000000-0000-0000-0000-000000000003', 'DEMO-FOOD-003', 'Mercimek Corbasi', 'Portion', 'adet', 'f0000000-0000-0000-0000-000000000001'),
  ('f1000000-0000-0000-0000-000000000004', 'DEMO-DRNK-001', 'Ayran',            'Portion', 'adet', 'f0000000-0000-0000-0000-000000000001'),
  ('f1000000-0000-0000-0000-000000000005', 'DEMO-DRNK-002', 'Salgam',           'Portion', 'adet', 'f0000000-0000-0000-0000-000000000001'),
  ('f1000000-0000-0000-0000-000000000006', 'DEMO-DRNK-003', 'Cay',              'Portion', 'adet', 'f0000000-0000-0000-0000-000000000001')
ON CONFLICT DO NOTHING;

INSERT INTO inventory.product_stock_mappings (product_id, stock_item_id, quantity_multiplier) VALUES
  ('d0000000-0000-0000-0000-000000000001', 'f1000000-0000-0000-0000-000000000001', 1),
  ('d0000000-0000-0000-0000-000000000002', 'f1000000-0000-0000-0000-000000000002', 1),
  ('d0000000-0000-0000-0000-000000000003', 'f1000000-0000-0000-0000-000000000003', 1),
  ('d0000000-0000-0000-0000-000000000004', 'f1000000-0000-0000-0000-000000000004', 1),
  ('d0000000-0000-0000-0000-000000000005', 'f1000000-0000-0000-0000-000000000005', 1),
  ('d0000000-0000-0000-0000-000000000006', 'f1000000-0000-0000-0000-000000000006', 1)
ON CONFLICT (product_id, stock_item_id) DO NOTHING;

INSERT INTO inventory.stock_movements
  (stock_movement_id, stock_item_id, stock_location_id, movement_type, direction, quantity, unit_code, source_type, reason)
SELECT m.movement_id::uuid, m.stock_item_id::uuid, 'f0000000-0000-0000-0000-000000000001', 'Adjustment', 'In', 500, 'adet', 'Manual', 'Demo opening stock'
FROM (VALUES
  ('f2000000-0000-0000-0000-000000000001', 'f1000000-0000-0000-0000-000000000001'),
  ('f2000000-0000-0000-0000-000000000002', 'f1000000-0000-0000-0000-000000000002'),
  ('f2000000-0000-0000-0000-000000000003', 'f1000000-0000-0000-0000-000000000003'),
  ('f2000000-0000-0000-0000-000000000004', 'f1000000-0000-0000-0000-000000000004'),
  ('f2000000-0000-0000-0000-000000000005', 'f1000000-0000-0000-0000-000000000005'),
  ('f2000000-0000-0000-0000-000000000006', 'f1000000-0000-0000-0000-000000000006')
) AS m (movement_id, stock_item_id)
ON CONFLICT (stock_movement_id) DO NOTHING;

INSERT INTO inventory.stock_balances (stock_balance_id, stock_item_id, stock_location_id, on_hand_quantity, reserved_quantity, available_quantity) VALUES
  ('f3000000-0000-0000-0000-000000000001', 'f1000000-0000-0000-0000-000000000001', 'f0000000-0000-0000-0000-000000000001', 500, 0, 500),
  ('f3000000-0000-0000-0000-000000000002', 'f1000000-0000-0000-0000-000000000002', 'f0000000-0000-0000-0000-000000000001', 500, 0, 500),
  ('f3000000-0000-0000-0000-000000000003', 'f1000000-0000-0000-0000-000000000003', 'f0000000-0000-0000-0000-000000000001', 500, 0, 500),
  ('f3000000-0000-0000-0000-000000000004', 'f1000000-0000-0000-0000-000000000004', 'f0000000-0000-0000-0000-000000000001', 500, 0, 500),
  ('f3000000-0000-0000-0000-000000000005', 'f1000000-0000-0000-0000-000000000005', 'f0000000-0000-0000-0000-000000000001', 500, 0, 500),
  ('f3000000-0000-0000-0000-000000000006', 'f1000000-0000-0000-0000-000000000006', 'f0000000-0000-0000-0000-000000000001', 500, 0, 500)
ON CONFLICT DO NOTHING;

COMMIT;

SELECT
  (SELECT count(*) FROM table_mgmt.zones)    AS zones,
  (SELECT count(*) FROM table_mgmt.tables)   AS tables,
  (SELECT count(*) FROM catalog.categories)  AS categories,
  (SELECT count(*) FROM catalog.products)    AS products;
