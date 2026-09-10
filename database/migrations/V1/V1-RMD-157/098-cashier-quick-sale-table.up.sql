-- V1-RMD-157: the cashier's quick-sale flow has always posted every order
-- against the fixed table id 00000000-0000-0000-0000-000000000001
-- ("KASA-1", src/Clients/Cashier/wwwroot/cashier-app.js), but nothing in the
-- repository — no migration, no seed script, no test — ever created a
-- table_mgmt.tables row for it. orders.orders.table_id is FK'd to this
-- table, and Orders' own error mapper has no ForeignKeyViolation-specific
-- branch (unlike Catalog's), so every quick sale on a genuinely fresh
-- deployment failed with a bare generic 503 database-error response,
-- telling the cashier the database was down when the real problem was a
-- missing row nobody had provisioned.
--
-- zone_id is deliberately NULL: this is not a seat on the floor plan, it is
-- a technical anchor point the quick-sale flow needs to satisfy the FK.
INSERT INTO table_mgmt.tables (table_id, zone_id, table_number, capacity, active, current_status, row_version)
VALUES ('00000000-0000-0000-0000-000000000001', NULL, 'KASA-1', 0, TRUE, 'Available', 1)
ON CONFLICT (table_id) DO NOTHING;
