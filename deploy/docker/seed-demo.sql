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

COMMIT;

SELECT
  (SELECT count(*) FROM table_mgmt.zones)    AS zones,
  (SELECT count(*) FROM table_mgmt.tables)   AS tables,
  (SELECT count(*) FROM catalog.categories)  AS categories,
  (SELECT count(*) FROM catalog.products)    AS products;
