-- V1-RMD-130: widens kitchen.print_jobs.status to allow AwaitingOperatorReview
-- (PrintJob.MarkAwaitingOperatorReview) — the terminal, never-auto-retried
-- state a print job enters when its transport connected and may have
-- transmitted before failing (ambiguous outcome, PDF:I.16-I.20). Retrying
-- automatically in that case risks a duplicate physical print in the
-- kitchen; only an operator's explicit reprint decision (already modeled by
-- kitchen.physical_print_deliveries / IPhysicalPrintRecoveryService) can move
-- the ticket forward.
ALTER TABLE kitchen.print_jobs
    DROP CONSTRAINT print_jobs_status_check;

ALTER TABLE kitchen.print_jobs
    ADD CONSTRAINT print_jobs_status_check
    CHECK (status IN ('Pending', 'Leased', 'Printing', 'Printed', 'Failed', 'DeadLetter', 'Cancelled', 'AwaitingOperatorReview'));
