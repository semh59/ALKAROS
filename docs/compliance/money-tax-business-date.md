# Money, Tax and Business Date Rules

> **Task:** V0-CMP-002
> **Status:** Done
> **Assignee:** codex-v0-cmp-002
> **Work type:** decision
> **Source basis:** PDF:II.0-II.1, PDF:III.0-III.2
> **Date:** 2026-07-30

## 1. Tax Rules

### Inclusive/Exclusive Tax
- All prices in the system are stored **inclusive of VAT (KDV)**.
- Tax is calculated at line level, not bill level.
- Standard KDV rate: %10 (restaurant food service). Reduced rate: %1 (certain items). Rate is determined by product category.

### Line-to-Bill Rounding
- Each line item total is rounded to 2 decimal places (kuruş).
- Bill total = SUM of rounded line totals.
- No additional rounding at bill level.

### Refund Rounding
- Refund amounts use the same rounding as the original line items.
- Partial refunds are rounded to 2 decimal places per refund line.

## 2. Discount Distribution

- Discounts are applied proportionally across all line items in the Bill.
- Discount amount per line = (line_total / bill_total) * discount_amount.
- Each discounted line total is rounded to 2 decimal places.
- The sum of discounted line totals MUST equal the original bill total minus the discount (within 1 kuruş tolerance).

## 3. Currency Rules

- Base currency: TRY (Turkish Lira).
- All monetary values stored as NUMERIC(12,2).
- Foreign currency payments are converted to TRY at the rate valid at the time of payment.
- Conversion rate is stored with the payment record for audit.

## 4. Business Date Rules

### Timezone
- All timestamps stored in UTC.
- Business date is determined by the store's local timezone (Europe/Istanbul, UTC+3).
- Business date = local date at the time of the transaction.

### Service Day Cutoff
- The service day (iş günü) starts at 06:00 local time and ends at 05:59 local time the next day.
- A transaction at 04:00 local time belongs to the PREVIOUS service day.
- A transaction at 06:30 local time belongs to the CURRENT service day.
- This affects: cash session closure, daily fiscal reports (Z Report), and daily sales reporting.

### Midnight Crossing
- If a bill is opened before midnight and settled after midnight, the entire bill belongs to the service day when it was opened.
- Exception: If the bill crosses the 06:00 cutoff, it MUST be split into two bills.

## 5. Invariants

1. **Tax consistency**: Same basket across all channels produces the same payable/tax result.
2. **Rounding closure**: All example calculations close at the kuruş level (no rounding drift).
3. **Service day integrity**: No transaction belongs to more than one service day.
4. **Cutoff enforcement**: Bills crossing the 06:00 cutoff MUST be split.

## 6. Positive Examples

### Example 1: Simple tax calculation
- Item price: 110.00 TL (inclusive of %10 KDV)
- KDV amount: 10.00 TL
- Net amount: 100.00 TL

### Example 2: Discount distribution
- Bill total: 200.00 TL (items: 120 + 80)
- Discount: 20.00 TL (%10)
- Item 1 discount: (120/200) * 20 = 12.00 TL
- Item 2 discount: (80/200) * 20 = 8.00 TL
- Item 1 after discount: 108.00 TL
- Item 2 after discount: 72.00 TL
- Bill total after discount: 180.00 TL ✅

## 7. Negative Examples

### Example 1: Midnight crossing without split
- Bill opened at 23:30, settled at 00:45 (same service day, before 06:00 cutoff)
- Result: Allowed — same service day ✅

- Bill opened at 23:30, settled at 06:30 (crosses 06:00 cutoff)
- Result: MUST be split into two bills ❌

## 8. Affected Tasks

- V1-CAT-001 (Product catalog pricing)
- V1-BIL-001 (Bill foundation)
- V12-PAY-001 (Payment aggregate)