# Card Tip Scope Decision — approved decision record

> **Task:** V13-GOV-004
> **Status:** Done
> **Work type:** decision
> **Source basis:** PO:2026-09-17
> **Access date:** 2026-09-17
> **Approver:** Semih — 2026-09-17
> **Decision type:** Business decision (named approver)

## Selected decision

Card-based tip capture is **out of scope for V1.3**. Cash tip continues
exactly as already approved by `V0-CMP-004` (optional, customer-initiated,
recorded as a separate non-fiscal line, not distributed via payroll) — this
decision does not change that. No `TenderMethod`, `Payment`, or terminal
adapter task in V1.3 adds a tip-capture field or flow for `BankCard`/
`MealCard` tenders.

## Why

Two independent reasons, either one alone would be sufficient:

1. **No technical surface today.** `developer.tokeninc.com`'s public
   `IntegrationHub.dll`/`TokenX Connect` documentation shows a payment-type
   (`type`) and meal-card-operator (`operatorId`) selection, but no
   tip-specific field or flow. Building against an undocumented surface
   risks guessing wrong, and `V0-HUG-001` (Token/Beko integration contract
   validation) has not yet produced real device evidence either way.
2. **No settled legal framework.** Turkey's regulatory framework for
   card-based voluntary tipping ("Dijital Gönüllü Bahşiş Sistemi") had not
   been finalized as of this decision (2026-09-17, per the approver's own
   knowledge). This is the same kind of fast-moving regulatory area that
   already produced a real, dated correction elsewhere in this codebase (a
   2026-01-30 ban on mandatory service/kuver charges) — building a card-tip
   flow ahead of the rule risks either violating it once it lands, or
   needing to be torn out and rebuilt.

## Rejected alternative

Implementing card tip now against Token's currently-documented surface,
with the assumption it can be "adjusted later" — rejected: a live legal
framework not yet published cannot be assumed compatible with a
speculative implementation, and no technical contract exists yet to build
against safely.

## Re-evaluation trigger

This decision should be revisited once **both** of the following are true:

- `V0-HUG-001` (Token/Beko integration contract validation) has real
  device/sandbox evidence of a documented tip-capture surface, and
- Turkey's Digital Voluntary Tip System (or equivalent) regulatory
  framework has been published and its requirements are known.

Until then, card tip is not designed, not stubbed, and not referenced by
any `TenderMethod`/`Payment` field.

## Invariants for consumers

- No V1.3 `Payment`/`TenderRequest`/terminal-adapter task adds a tip amount
  field for `BankCard` or `MealCard` tenders.
- Cash tip's existing rules (`V0-CMP-004`) are unaffected by this decision.

## Affected tasks

- Depends on: `V0-CMP-004` (cash tip's existing, unaffected rules).
- Consumers: any future `V13-PAY-*`/`V13-HUG-*` task must NOT add card-tip
  capture until the re-evaluation trigger above is met.

## Acceptance evidence

- Decision record with source, approver, rationale and re-evaluation
  trigger: above.
