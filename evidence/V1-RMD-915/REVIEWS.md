# V1-RMD-915 reviews

## qa-backend
- No finding: NULL handling equals the old behaviour (outer COALESCE), bill filter matches BillMoneyGuard, merged-bill grouping does not double count, no other reader of paid money still ignores refunds, no regression.
- Edge (low): refund without any allocation cannot occur in practice.

## qa-test-kalitesi
- Missing full-refund case: added (second refund brings the shown amount to 0). Unrefunded bills stay covered by the existing queue test (100); other bill's refund leak killed by mutant 1.
- Not covered by design: refund larger than paid (the ledger guard forbids it), bill without allocations (unchanged behaviour).
