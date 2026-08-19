ALTER TABLE kitchen.physical_print_deliveries
    DROP CONSTRAINT IF EXISTS physical_print_deliveries_status_check;

ALTER TABLE kitchen.physical_print_deliveries
    ADD CONSTRAINT physical_print_deliveries_status_check
    CHECK (status IN ('InFlight', 'Printed', 'Unknown', 'ReprintApproved', 'ReprintInFlight', 'ReprintRejected', 'Reprinted'));
