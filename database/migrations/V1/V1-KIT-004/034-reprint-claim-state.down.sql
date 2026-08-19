ALTER TABLE kitchen.physical_print_deliveries
    DROP CONSTRAINT IF EXISTS physical_print_deliveries_status_check;

-- An in-flight operator reprint is physically uncertain during rollback;
-- preserve the fail-closed review state before restoring the old enum set.
UPDATE kitchen.physical_print_deliveries
SET status = 'Unknown',
    is_reprint = FALSE,
    reprint_payload = NULL,
    resolved_at = NULL
WHERE status = 'ReprintInFlight';

ALTER TABLE kitchen.physical_print_deliveries
    ADD CONSTRAINT physical_print_deliveries_status_check
    CHECK (status IN ('InFlight', 'Printed', 'Unknown', 'ReprintApproved', 'ReprintRejected', 'Reprinted'));
