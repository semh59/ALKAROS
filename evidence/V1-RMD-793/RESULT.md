# V1-RMD-793 - Result

Refund backend (G4, refund side): the refund method is now read from real records, a cash refund is paid from the till of the terminal it was asked at, and the shared "manager may approve their own request" setting covers refunds, refund allowances and the "card was charged" claim.

## What changed
- Refund method: `PaymentMethodLookup` (new, one definition for the refund flow and the receipt) returns Account, Cash, Card (approved card attempt or approved manual card confirmation) or Eft (what is left; the producers of approved payments are a closed set, written in the code comment). `refund_requests.method` is `Cash | Card | Eft`; migration 215 back-fills old `Manual` rows with the same rule and drops the value. The receipt no longer shows a manually confirmed card as "EFT / diger".
- Completion: Card needs the slip number; Eft needs no bank reference (EFT approval stays unverified by PO decision 2026-09-16).
- Terminal/session: the request stores `terminal_id`; cash refunds, the refund allowance (read and grant; grant body now carries `CashierTerminalId`) use the open session of (terminal, user). No "one active session per user" rule was added. Old requests without a terminal are refused with `NO_OPEN_CASH_SESSION` and asked again.
- Self approval: the existing `approvals.manager_self_approval_enabled` setting (no second definition). Off: a second person is required and the Turkish message says so. On: the requester approves/rejects their own refund, grants themselves an allowance, or approves their own card claim; the row is marked (`approval_mode = 'Self'`, `manual_card_confirmations.self_approved`), the audit events carry `selfApproved`, and the refund list filters with `?selfApproved=`. Constraints in migration 215 allow exactly these cases (`ck_manual_card_self_approved`).
- Session lock (scope c): already in HEAD (`RecordAsync` takes `FOR SHARE` on the open session, close takes `FOR UPDATE`, V1-RMD-790). This task proves the new terminal-scoped approve path and the allowance-spend path against a closing session with real-Postgres race tests, and that a partial cash refund lowers the expected cash (F0917).

## Closed at HEAD (no code change, evidence cited)
- Row 11 EFT replay amount: `EftAmountMismatchException` (`EftTenderHandler.cs:99-104`).
- "Slip validated before status": status check first, test `CompletingARefundThatIsNotAwaitingTheManualStep...`.
- F0389 cash movement Notes replay: V1-RMD-788 (`CashMovementStore.cs:160`).

## Evidence
- Real Postgres (docker alkaros-test-pg): Billing project 144/144 (`r9-billing`; one test-only exception-type assertion fixed and re-run green in `r10-self`), PaymentTender 67/67 (`r11-tender`; the one failure was a "False" vs "false" string in the new test, re-run green in `r12-rej`), Reconciliation 34/34, ManifestTests 17/17, ProductMargin 20/20, ApiConventions 17, ModuleBoundaries 17, Host.Errors 224. Skipped tests: none.
- New/extended tests: method derivation incl. negative cases (rejected claim, declined attempt), transfer completed without slip under an allowance, migration back-fill and the ambiguous two-terminal case run against the real SQL files (down, legacy rows, up), terminal-matched session and allowance, grant-versus-close and approve-versus-count races (lock polled in pg_stat_activity, no sleeps), other-terminal approval, exact partial-refund cash amount, self approval on/off for refund, reject, allowance (own and for another person) and card claim (approve and reject), audit flags, list filter, constraint tests.
- Migration 215 forward, backward, forward and a second forward: `migration-forward-backward.log`.
- Red: the new tests use columns, endpoints and parameters that did not exist before, so they cannot compile against the old code; the red/green proof is the mutation run (each rule broken -> a test fails).
- Mutation (`mutation-summary.txt`): 20 valid mutants, all killed, every file restored with `cmp`. Two rounds were invalid and are not counted: m12 (compile error, replaced by m12b) and a first m18 run whose filter matched the wrong tests (survived because the lock test was not in the filter; re-run with the correct tests -> killed). Numbers are not contiguous because dropped ideas kept their label.
- plan-audit / consistency-audit and the task-scope tool: see the closing commit.

## Open risk
- Old `Requested` cash requests that cannot be back-filled to a terminal must be asked again (explicit 409, nothing moves).
- The gün sonu report has no "self approved" counter (refund list filter and audit are the report); no UI in this task (V1-RMD-795).
