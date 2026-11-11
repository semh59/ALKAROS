# V1-RMD-933 Result

The payment screen now has Turkish sentences for the discount type, undo, missing idempotency key, refund fallback and other-till bill errors (worded exactly like the server), and passes the server's own Turkish sentence through for the five codes whose sentence carries this request's numbers or reason (discount value/reason, tip amount, note length, refund allowance, refund text). The lock test now scans BillingSplitStore.cs (every exception code, not only readable ones), the refund input exceptions, the other-till constant and the refund fallback; merge codes are an explicit list and an unlisted merge code fails the test.
Also closes the follow-up from the card terminal task: after a refused card payment the method selection is reset, asserted through the method hint (mutant killed).
Red: lock test failed with 11 missing sentences. Green: StaticApps 41 files / 459 tests. Mutants: 8 killed (mutations.txt), restored (cmp ok).
