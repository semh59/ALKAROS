-- Migration 059 Down: Rollback Stock Item and Stock Location Master Data (V11-INV-004)
DROP TABLE IF EXISTS inventory.product_stock_mappings;
DROP TABLE IF EXISTS inventory.stock_items;
DROP TABLE IF EXISTS inventory.stock_locations;
