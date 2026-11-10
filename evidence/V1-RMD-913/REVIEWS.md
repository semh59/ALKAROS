# V1-RMD-913 reviews

## qa-dokuman-bagimlilik
- Claims verified against code (730, 100, 6 hours, two delete rules, no ledger/balance/invoice change, security-screen run via the generic maintenance endpoint).
- Rule 2 wording corrected: write side counts the last ledger row up to that day's cut-off, delete side counts today's last row.
- Cross-reference to the housekeeping verb and the 5-10 year classes: one sentence added (data-housekeeping.md is outside this task's surface).

## qa-mevzuat-uyum
- Code description correct. Added: the section does not shorten ledger or invoice retention, does not change the customer profile window, and the boundary follows the business-day calendar.
- OPEN PRODUCT POINT (not decided here): the runbook keeps customer profiles 10 years after the last ledger movement, while the product decision (2026-11-01) says non-invoice customer data is not kept beyond 2 years after the last transaction. Whether the profile window matches that decision needs a product-owner answer.
