# V1-RMD-794 - Result

Tender backend (G4, collection side).

## What changed
- F0409: a cash tender replay now also compares the amount handed over (`ApprovedAmount + ChangeAmount` against `TenderedAmount`). Same key with another amount -> Turkish 409 `TENDER_IDEMPOTENCY_KEY_REUSED`, nothing written; the identical request still replays (existing replay test).
- A tender refused for exceeding the remaining amount (card, EFT, cash, account charge, manual "card was charged" approval) leaves one `payment.tender-rejected` audit event (bill, method, amount, remaining, terminal, key or claim id, cashier). The write has its own 5-second deadline, the key is cut to 100 characters, and a failing audit store is logged as an error (event 6943) while the cashier still sees the same 409. Each refused attempt leaves its own event (a retry is a second attempt).
- Client limit locks (`ClientLimitLockTests`): the five numeric ranges on the management settings screen are the exact set of server rules and are run against the real server validator (min/max accepted, one outside refused); the cash screen note limits (7 places) equal the server constant; the refund screens (React and cashier) state only the server refund text and slip lengths; the card-claim slip length of the payment screen is the longest slip the endpoint accepts (`ClaimSlipLimitHttpTests`).
- Turkish lock (`ErrorMessageLanguageTests`): 272 response messages of tender, cash, account and refund code are scanned for English words. None were English, so no message text changed.
- F0389 closed earlier in V1-RMD-788.

## Evidence
- Real Postgres (`w-*` and `z-*` folders): TenderHandler 14/14, CustomerAccounts 22/22, PaymentTender 76/76, CashSession 106/106 (includes the lock tests), ApiConventions 17, ModuleBoundaries 17, Host.Errors 224. Skipped: none.
- Red (`red-*`): the replay test and both audit tests failed before the fix. The limit locks, the language scan and the "audit write fails" test are locks that pass on first run; their strength is shown by mutation.
- Mutation (`mutation-summary.txt`): 26 mutants (replay check removed; wrong method, amount, event name, terminal, actor and claim id; audit failure leaking; audit failure not logged; long key not cut; client ranges changed up, down and for minutes; refund text and slip limits changed; claim slip limit changed; refund slip limit on the React screen changed; server note limit changed; English message in a tender, a refund and an access-denied message; audit write tied to no own deadline), all killed; files restored with `cmp`. Runs that failed to compile or collided with a parallel run were deleted and repeated; only clean runs are listed.
- Reviews: qa-backend (audit missing on cash, manual approval and account paths, token handling, event shape -> all fixed) and qa-test-kalitesi (log never verified, lock coverage of refund screens and rules, scan reach, missing mutants -> all fixed) and a verification round (terminal and actor checks, own deadline, wider scan, discovered refund screens -> fixed).

## Open risk
- None for the backend. Screens that show these results are V1-RMD-795.
