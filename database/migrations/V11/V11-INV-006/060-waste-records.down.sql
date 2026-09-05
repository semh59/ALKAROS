-- Migration 060 rollback: Drop waste_records table and triggers
DROP TRIGGER IF EXISTS trg_waste_records_immutable ON inventory.waste_records;
DROP FUNCTION IF EXISTS inventory.prevent_waste_record_mutation();
DROP TABLE IF EXISTS inventory.waste_records;
