# V1-RMD-916 result

Decision (Product Owner, 2026-11-10): a customer profile without an invoice is anonymized 2 years after the last transaction; a profile tied to an invoice keeps 10 years.

Verified in code first: profile anonymization exists (V15-KVK-001 selection, work items, step-by-step workflow in Program.cs). It used one 10-year window for everyone. Changed:
- New retention class InvoicedCustomerProfile; CustomerProfile now selects only customers with no invoice. Open balance and pending-request protection unchanged for both.
- Migration 225: widens the class check on retention_rules, legal_holds, retention_work_items and privacy.anonymization_jobs (the last one was found by the apply test), the completeness trigger counts 6 classes, and publishes the next policy version (2 here): it copies the latest windows, sets CustomerProfile to 2 and gives InvoicedCustomerProfile the old customer window. Policy 1 is untouched.
- kvkk-retention: both customer classes anonymize the same fields; the summary line counts them together as customers=N. A profile queued as one without an invoice waits (blocked=N, never anonymized) when an invoice appeared before the apply; found by the compliance review. Its stale pending item stays blocked; when the 10 years pass the customer is selected again as an invoiced profile with its own work item.
- Runbook: customer section rewritten with both windows and the note that this replaces the 10-year customer row of the V0-CMP-003 inventory; DisposalMatrix (security module) still says 10 years for customer data but nothing registers customers with it.

Tests: retention execution 15/15 (3 new: two windows with due dates, published policy values, legal hold on the new class), Host kvkk-retention 7/7 (two new end-to-end applies: queued-then-invoiced waits; plain 2y3m anonymized, 1y9m kept, invoiced 10y3m anonymized, invoiced 3y kept), anonymization workflow 13/13, manifest and rollback/reapply schema test green. Mutations: 6 killed.
