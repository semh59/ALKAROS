ALTER TABLE kitchen.print_jobs
    DROP CONSTRAINT print_jobs_status_check;

ALTER TABLE kitchen.print_jobs
    ADD CONSTRAINT print_jobs_status_check
    CHECK (status IN ('Pending', 'Leased', 'Printing', 'Printed', 'Failed', 'DeadLetter', 'Cancelled'));
