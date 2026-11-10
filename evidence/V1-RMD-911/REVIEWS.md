# V1-RMD-911 reviews

## qa-backend
- Diff clean: helper equals the old comp query, four call sites share it, locks and error mapping unchanged.
- Finding: CashierHandoffStore.GetChecksAwaitingPaymentAsync shows paid_amount without reversals (display only). Out of this task's surface; opened as V1-RMD-915.

## qa-test-kalitesi
- Weak assert on the manager-needing pre-check: now asserts 202 Accepted.
- Missing: part-refund undo refusal and boundary, other bill's refund leakage, sign: added two tests, mutants 6 and 7 killed.
- Comp path is covered by Orders.Comp (mutant 5), not this file; stated in RESULT.
