# V1-RMD-975 Result

Status: Done (server side only).

- POST /api/v1/management/inventory/stock-items/{id}/transfers moves stock between two locations as a Transfer Out and a Transfer In ledger movement, in one transaction, with a required idempotency key (a repeat answers with the first transfer, the same key for a different transfer is refused), row locks in location id order, no negative or reserved-stock shortfall at the source, quantity positive with at most four decimals, reason required; the ledger check accepts the new Transfer type (migration 227); one leg of a transfer cannot be reversed alone.
- Tests: Inventory HTTP 47/47, reversal domain 12/12, migration/manifest 18/18, ApiConventions 2/2; 11 mutants killed.
- Not done in this task: the screen to start a transfer and lifting the default-location lock (both still open; consuming stock from a new default location needs the transfer first).
- Not captured: a separate red run (tests and code written in one pass; mutants prove each behaviour).
