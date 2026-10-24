DROP TABLE IF EXISTS table_mgmt.table_sent_checks;

ALTER TABLE table_mgmt.table_merges DROP CONSTRAINT IF EXISTS ck_table_merges_distinct_tables;
ALTER TABLE table_mgmt.table_reservations DROP CONSTRAINT IF EXISTS ck_table_reservations_expiry;
ALTER TABLE table_mgmt.table_reservations DROP CONSTRAINT IF EXISTS ck_table_reservations_party_size;

DROP INDEX IF EXISTS table_mgmt.ux_table_merges_active_participant;
DROP INDEX IF EXISTS table_mgmt.ux_table_reservations_active_table;
