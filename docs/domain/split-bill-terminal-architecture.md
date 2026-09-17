# Split-Bill Terminal Architecture — approved decision record

> **Task:** V13-GOV-003
> **Status:** Done
> **Work type:** decision
> **Source basis:** PO:2026-09-17
> **Access date:** 2026-09-17
> **Approver:** Semih — 2026-09-17
> **Decision type:** Business decision (named approver, market-research-driven)

## Selected model

Splitting a Bill (deciding who owes what) is computed **entirely inside
ALKAROS**, by the existing `SplitEngine` (`src/Modules/Billing/
SplitDesign/SplitEngine.cs`, already Done) — `CreateEqualSplit`/
`CreateAmountSplit`/`CreateItemSplit`/`CreateCustomSplit`, each producing a
list of `BillAllocation` (owner/segment + `AllocatedAmount` + `TaxAmount`).

The payment terminal (Token/Beko) **never receives a "split the total N
ways" instruction** and its own built-in split feature, if any exists, is
never used. Instead, once a split design is final, a future adapter task
sends the terminal **one separate basket + one separate payment request
per `BillAllocation`**, sequentially — from the terminal's point of view,
each split owner's payment is an entirely ordinary, single, non-split
sale.

## Why

`V0-ARC-004`'s own locked principle: PostgreSQL is the single source of
truth (SignalR, printer buffers, provider callbacks — and by the same
logic, a terminal's internal split state — are not). If ALKAROS handed the
whole bill total to the terminal and let its own UI drive the split:

- ALKAROS could not audit or reconstruct which owner paid which portion
  without trusting the terminal's own (opaque, vendor-controlled) split
  bookkeeping.
- A terminal firmware update changing split behavior would silently change
  ALKAROS's own financial correctness, with no code change on the ALKAROS
  side to review or test against.
- `PaymentAllocation` (`V13-ALC-001`) requires one `Payment` per tender
  attempt tied to a known `Bill`/allocation; a terminal-native split
  produces an unknown number of provider-side sub-transactions ALKAROS
  cannot map back to its own `BillAllocation` rows.

`SplitEngine` already computes the correct, testable, owner-level amounts
in software (28/28 tests, `V1-BIL-004` family) — reusing that as the sole
source of "who owes what" and treating the terminal as a dumb, single-sale
executor per owner keeps the whole payment flow inside ALKAROS's own,
already-verified financial invariants.

## Examples

Positive: a ₺150 bill split equally 3 ways via `SplitEngine.
CreateEqualSplit` → three `BillAllocation` rows (₺50 each, rounding
remainder on the last per the existing kuruş invariant, V0-CMP-002).
The adapter sends three separate `TenderRequest`s (₺50 each) to the
terminal, one after another; the terminal only ever sees three ordinary
₺50 sales, never "split 150 by 3."

Negative (rejected alternative): sending the terminal a single "split ₺150
into 3 payments" instruction and letting its own UI collect three cards —
rejected: ALKAROS cannot verify the terminal actually collected exactly
₺150 total, cannot map the terminal's own receipts back to specific
`BillAllocation` rows without a private, undocumented terminal-side
correlation mechanism, and inherits whatever split rounding/UX behavior
the terminal vendor chose rather than ALKAROS's own kuruş-exact rule.

## Invariants for consumers

- A split design (`BillAllocation` rows) is always fully computed and
  persisted in ALKAROS *before* any terminal request is sent for it.
- Exactly one `TenderRequest`/terminal transaction exists per
  `BillAllocation` that is actually tendered — never a single terminal
  transaction covering multiple allocations, never a terminal-native
  split.
- The terminal adapter (a future task, not yet opened) is a pure
  translator: `BillAllocation` → one basket + one payment request. It
  never receives or interprets "how many ways to split."

## Affected tasks

- Depends on (already Done): `V1-BIL-004` family (`SplitEngine`,
  `BillAllocation`).
- Consumers (future implementation, not yet opened as a task): the
  split-bill-to-terminal adapter described in `V13-PAY-003`/
  `V13-HUG-001`/`V13-PUI-001`'s eventual scope.

## Acceptance evidence

- Decision record with source, approver, rationale and affected tasks:
  above.
- `SplitEngine`'s own 28/28 `ALKAROS.Billing.SplitDesign.Tests` already
  demonstrate the owner-level allocation output this decision relies on.
