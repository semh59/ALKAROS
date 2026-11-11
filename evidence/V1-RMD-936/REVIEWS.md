# V1-RMD-936 Reviews

## qa-frontend
Cashier clean. Low: PosTerminal billingApi.ts has one generic UNSUPPORTED_BILL_STATE sentence (follow-up, outside this task).

## qa-test-kalitesi
- Medium: tip refusal showed the discount sentence -> fixed (server sentence covers discount and tip; server tests assert the message; tip screen test added).
- Medium: PER_ACTION_CODES could skip silently -> lock test now requires at least 4 distinct server sentences (M5 kills it).
- Medium: split/cancel/comp screen tests missing -> all go through the same pass-through path (M4 kills the tip and undo tests, M2 the discount test); a per-action screen test for split and cancel adds no distinct path.
- Low: discount negative assertion narrow -> accepted, M2 covers it.
