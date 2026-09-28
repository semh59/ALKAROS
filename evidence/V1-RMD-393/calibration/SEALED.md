# SEALED - seeded calibration bug (do not show to auditor)

## Location
src/Modules/Billing/Adjustments/AdjustmentCalculator.cs:42 (inside `AdjustmentCalculator.Calculate`, Tip branch of the adjustment loop)

Change: `tipGross += adj.GrossAmount;` -> `tipGross = adj.GrossAmount;`

## What it does
When a bill carries more than one Tip adjustment, only the LAST tip (by `created_at ASC`, the order
`PostgresBillAdjustmentRepository.GetByBillIdAsync` returns) is counted. `TotalTips` and
`AdjustedPayableAmount` are understated by the sum of all earlier tips. Discounts and fees are unaffected;
a single tip behaves correctly.

`AdjustmentCalculator.Calculate` is the single source of the adjusted payable ceiling, so the error propagates to:
- Host bill payment summary / cashier "remaining amount" (DualScreenApplication.Payments.cs ~L152)
- Allocation ceiling (PostgresPaymentAllocationRepository.AllocateAsync -> PaymentAllocationFactory) and the
  fail-fast checks in CashTenderHandler / EFT tender
- Bill closure (BillPaymentClosureCalculator: bill is considered fully paid at the understated total)
- Split engine when called with an adjustment summary (equal/amount splits sized on the wrong total)
- Adjustment summary endpoint (`/billing/bills/{id}/adjustments` summary.adjustedPayableAmount, `/tip` response summary)

## Concrete scenario
Bill payable 200.00 TL. Two diners each add a tip via POST `/billing/bills/{id}/tip` with different
idempotency keys: 15.00 TL then 10.00 TL.
- Expected: TotalTips 25.00, AdjustedPayableAmount 225.00.
- Actual: TotalTips 10.00, AdjustedPayableAmount 210.00.
Consequences: cashier's remaining shows 210.00; a tender for 225.00 is rejected as over-allocation
("Tutar kalan 210.00 TL'yi asiyor"); the bill closes as fully paid after 210.00, so 15.00 TL of recorded
tip is never collected / never allocated, while bill_adjustments rows still total 25.00 (tip-pool / waiter
reporting vs collected money mismatch).

## Existing tests that would catch it
None that I could find. Every existing test uses at most one Tip adjustment per bill:
- tests/Modules/Billing/Adjustments/AdjustmentsDomainTests.cs `AdjustmentCalculatorComputesTotalsAccurately`
  (one discount + one fee + one tip -> still 520.00, passes)
- tests/Modules/Cash/TenderHandler/CashTenderHandlerTests.cs and tests/Modules/Payments/EftTender/EftTenderHandlerTests.cs
  (one tip each)
- tests/Host/Experience/Billing/BillingSplitHttpTests.cs tip tests (single tip; the idempotent-retry test
  asserts only 1 row is stored, so the calculator never sees two tips)
- tests/Modules/Billing/PaymentClosure/BillPaymentClosureCalculatorTests.cs uses only a discount.
A test with two distinct tips on one bill (or tip + tip then summary/remaining/closure assertion) would catch it.
