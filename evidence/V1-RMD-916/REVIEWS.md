# V1-RMD-916 reviews

## qa-mevzuat-uyum
- HIGH, fixed: a profile queued as no-invoice that got an invoice before the apply was still anonymized (the apply guard checked only the balance). The no-invoice plan now also blocks on an invoice; test AProfileQueuedWithoutAnInvoiceWaitsWhenAnInvoiceAppearsBeforeTheApply, mutant 6 killed. The invoiced class keeps the balance-only guard.
- Pre-existing, not changed: a pending anonymization request is checked only at selection, not again at apply.
- PRODUCT POINTS (not decided here): "faturalı" is read as any row in invoicing.invoices with the customer id (draft and cancelled included, conservative; an invoice without a customer id does not count); "last transaction" is the last ledger movement or invoice, orders are not counted.

## qa-veritabani
- No finding: all four tables with a retention class check are widened (retention_rules, legal_holds, retention_work_items, privacy.anonymization_jobs), constraint names match the generated ones, the policy-publishing CTE satisfies the deferred completeness trigger, copies the latest windows (operator-published versions included) and is idempotent, down handles the immutability triggers and refuses once the new class was used.
- Operators who published their own policy get a new version with CustomerProfile at 2 years: customers without an invoice become due immediately; this is the intended effect and should be announced.
- order.json tables field names only retention_rules (the migration touches four tables); same convention as other entries.
