# V1-RMD-935 Result

The end-of-day open bill count (preview, close response, stored close record and audit) now also counts live (Submitted, Accepted, Preparing, Ready, Served) orders that have no non-cancelled bill, any source, the same rule as the shift close. A bill-backed order is not counted twice; an order whose only bill was cancelled still counts.
Red: new test failed (expected 3, got 1). Green: Reporting project 36/36. Two older close tests that seeded live bill-less orders now acknowledge the open items (their purpose is unchanged). Mutants: 9 killed (mutations.txt), restored (cmp ok).
