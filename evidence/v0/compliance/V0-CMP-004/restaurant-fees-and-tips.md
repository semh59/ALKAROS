# Restaurant fees and tips — approved decision record

> **Task:** V0-CMP-004
> **Status:** Done
> **Work type:** validation
> **Source basis:** CORR:C10
> **Access date:** 2026-08-02
> **Approver:** Semih — 2026-08-03
> **Decision type:** Business decision (named business approver)

`CORR:C10` shows the PDF defines no fee/tip policy; it is not a source for
tax or product behavior. The following per-fee decisions are approved by the
named business approver and close the fee/tip applicability gap.

## Selected decisions

| Fee type | Result | Reason |
| --- | --- | --- |
| Tip (`tip`) | Optional, customer-initiated; recorded as a separate non-fiscal line (not a service-fee line); not distributed via payroll | Tip is a gratuity, not a restaurant revenue; fiscal output keeps it separate and non-taxable as a transfer item |
| Service fee | Not applied — no service fee on any bill | PDF and business practice apply no mandatory service fee; charging one would create revenue the PDF does not model |
| Table fee / masa ücreti | Not applied | No evidence in PDF; charging requires menu disclosure and is rejected |
| Cover charge (kuver) | Allowed only when advertised on the menu (`kuver` line); otherwise not applied | Consumer-disclosure principle; kuver is an explicit priced line when advertised |
| Payment/fiscal treatment | Tip and kuver appear as bill lines with their own `line_type`; only service items produce VAT-able lines | `line_type` catalog (V0-DAT-002) selects tax code; tip uses a non-taxable code |
| Payroll / distribution | No payroll integration; tip distribution is out of scope | `CORR:C10` does not authorize payroll behavior |

## Rejected alternatives

- Mandatory service fee — rejected: no PDF basis and business policy applies
  none.
- Tip as taxable service line — rejected: tip is a customer gratuity, not
  service revenue; fiscal output would misstate taxable base.
- Automatic kuver on every table — rejected: cover charge requires menu
  disclosure; silent kuver violates consumer expectations.
- Payroll/personel dağıtım kuralı seçmek — rejected: out of scope, no source.

## Invariants (consumers)

- A tip line never affects the service-day taxable total.
- A kuver line exists on a bill only when the menu advertises kuver for that
  service context.
- No fee is invented: bill lines for fees exist only for `tip` (optional,
  customer-initiated) and advertised `kuver`.

## Task status

- Status: `Done` — decision approved by named business approver on
  2026-08-03; `V0-CMP-002` dependency resolved in the same batch.
