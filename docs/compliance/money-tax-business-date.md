# Money, Tax and Business Date — approved decision record

> **Task:** V0-CMP-002
> **Status:** Done
> **Work type:** decision
> **Source basis:** PDF:II.0-II.1, PDF:III.0-III.2
> **Access date:** 2026-08-02
> **Approver:** Semih — 2026-08-03
> **Decision type:** Business decision (official tax source + named business approver)

PDF schema text specifies `numeric(18,2)` for all monetary values
(`III.1.5 Money`: "All monetary values use numeric. numeric(18,2) for
money; ... Never float/double for money"), UTC timestamps (`III.1.4
Timestamps`), and PostgreSQL 18 with UTC timestamps (`III.37`). It does not
select the restaurant's tax rates, tax-inclusive pricing policy, business
timezone, service-day cutoff or bill-splitting policy.

## Selected decisions

| Rule | Selected result | Official source / basis |
| --- | --- | --- |
| Default KDV rate for restaurant/cafe food and non-alcoholic beverage service | `%10` (tax code `10`) | 2007/13033 sayılı BKK, Liste II sıra 24; 7346 sayılı Cumhurbaşkanı Kararı (10/07/2023) — alkolsüz yeme-içme hizmeti %10 |
| Alcoholic beverage service rate | `%20` (tax code `20`) | 2007/13033 sayılı BKK, Liste I sıra 1 — alkollü içkiler %20 |
| Basic food (unprocessed) retail | `%1` (tax code `1`) | 2007/13033 sayılı BKK, Liste I sıra 4 — temel gıda %1 |
| Tax policy | Tax-inclusive line prices; tax computed per line and shown separately on fiscal output | POS menu prices are displayed tax-inclusive (restaurant practice); PDF `numeric(18,2)` keeps per-line sent-level tax |
| Money storage | `numeric(18,2)` in TRY (`TRY` currency code), never float/double | PDF `III.1.5 Money`, `III.37` |
| Timezone | Business timezone `Europe/Istanbul`; timestamps stored UTC | PDF `III.1.4 Timestamps`, `III.37` |
| Service-day cutoff | 23:59:59 local (`Europe/Istanbul`) — an order is billed on the service day in which it is created | Selected with named business approver; no PDF conflict |
| Rounding | Per-line round-half-up to kuruş; bill total is the sum of rounded line amounts (no re-rounding) | PDF `numeric(18,2)` per field; avoids drift across channels |
| Refund rounding | Refund lines use the same per-line round-half-up rule as the original lines | Same invariant as billing |
| Discount distribution | Discount distributed proportionally to line totals; each line amount stays `numeric(18,2)` with per-line rounding | Selected with named business approver |
| Bill-splitting | Split lines carry their own proportional tax lines; no cross-bill re-rounding | Same per-line invariant |

## Rejected alternatives

- `%18`/`%8` legacy rates — replaced by the 7346 Karar (10/07/2023) which
  lowered food-service KDV to `%10`; using legacy rates would misstate fiscal
  output.
- Tax-exclusive displayed prices — rejected: menu prices are shown
  tax-inclusive; the fiscal output still separates tax lines.
- Rounding at bill level only — rejected: line-level `numeric(18,2)` fields
  force per-line rounding; bill-level-only rounding produces lines that do
  not sum to the bill.
- Floating-point money (float/double) — forbidden by PDF `III.1.5`.
- Server-local timezone for service day — rejected: business is anchored to
  `Europe/Istanbul`; UTC storage keeps timestamps portable.

## Invariants (consumers)

- `V1-CAT-001`, `V1-BIL-001`, `V13-PAY-001`: the same basket must produce the
  same payable/tax result on every channel, closed at kuruş level.
- Tax codes are data (`tax_code` values `1`, `10`, `20`), selectable per
  `line_type`; a code change does not change the rounding rules.
- `service_date` is derived from `created_at` in `Europe/Istanbul`; an order
  created after midnight belongs to the previous service day only when the
  business cut-off says so (23:59:59 local cut-off).

## Examples

- Basket with one line `100.00 TRY` (food, `%10`): tax line `9.09`,
  net `90.91`, payable `100.00` (inclusive policy).
- Basket `100.00` split into `33.33`/`33.33`/`33.34` lines: each line's tax
  is rounded half-up independently; the sum of the three payable lines
  equals the original payable.
- Basket with one line `9.99` (food, `%10`): tax line `0.91`, net `9.08`,
  payable `9.99`.
- Negative example: `33.33 + 33.33 + 33.33 = 99.99 != 100.00` — such a split
  is invalid; the last line takes the remaining kuruş (`33.34`).

## Task status

- Status: `Done` — decision approved with official tax source and named
  business approver on 2026-08-03. Blocker resolved: the record no longer
  selects rates/timezone/cut-off without a source; every selected value now
  carries a source and an approver.

## Operational business day and tips (as built; the approved decision rows above are unchanged)

The approved "service-day cutoff" row describes the tax and invoice `service_date`: a calendar day in `Europe/Istanbul`.
The operational reports (end-of-day close, payment settlement report, channel report) use a different, operational
business day, and the code has always done so:

- The operational business day runs from 06:00 on the business date up to, not including, 06:00 the next day
  (`Europe/Istanbul`). A table still served at 01:00 belongs to the day the restaurant opened for. One definition,
  `BusinessDayWindow`, is used by all three reports; before it, the channel report alone cut at midnight.
- The two concepts are kept apart on purpose: `service_date` (tax, invoice) stays the calendar day; the operational day
  is a reporting window and never changes an invoice date.

Tips (gratuity):

- A tip is voluntary and is not part of the taxable amount; the end-of-day close stores it as its own line
  (`total_tips_amount`) and revenue excludes it.
- The tip total is the `Tip` adjustments of the bills closed (`Paid`) inside the window; a bill closed by a refund
  carries no tip in the total.
- Not confirmed: how a tip paid by card is treated for legal and accounting purposes has NOT been confirmed by a
  certified accountant (mali musavir). The code only provides the separate field and makes no accounting claim.
- The product margin report is a calendar-day report on the bill's opening date (its own tested definition); it is not an
  operational-day report and does not use the 06:00 window.
