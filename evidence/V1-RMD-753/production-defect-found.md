# V1-RMD-753 — test found a production defect (F0908-PD1)

Test: `BillingSplitHttpTests.ItemSplitAllowedOnAdjustedBillWhenNetAdjustmentIsZero`
Result: 400 `VALIDATION_FAILED` ("Hesap bölme isteği doğrulanamadı."), expected either success or a clear refusal.

Scenario (real PostgreSQL, real HTTP): tip 10 + fixed discount 10 on the same bill.

Root cause (source):
- Tip carries no tax: `src/Modules/Billing/Adjustments/BillAdjustment.cs:298` (`taxAmount: 0m`).
- Discount carries tax: `BillAdjustment.cs:167,208` (`taxAmount: discountTax`).
- Net payable is unchanged but adjusted tax is lower (`AdjustmentCalculator.cs:65`).
- Item-split guard compares payable only: `src/Host/Experience/Billing/BillingSplitStore.cs:546`.
- Engine item split uses each item's own tax (sum = Bill.TaxTotal): `src/Modules/Billing/SplitDesign/SplitEngine.cs:196,218`.
- Repository rejects the tax mismatch with InvalidOperationException: `src/Modules/Billing/SplitDesign/PostgresSplitDesignRepository.cs:334-335`.
- Exception filter maps InvalidOperationException to generic 400: `src/Host/Experience/Billing/BillingSplitApplication.cs:650`.

Effect: cashier gets a generic validation error instead of the 409 `ITEM_SPLIT_ON_ADJUSTED_BILL` message; item split on such a bill is impossible.

Fix requires production code (out of V1-RMD-753 scope: tests only). Decision needed:
A) Guard also compares tax (`AdjustedTaxTotal != Bill.TaxTotal`) -> clear 409, item split refused.
B) Item split distributes adjusted tax -> new allocation policy (product decision, V1-RMD-752 Out of scope notes item-level policy is undecided).

Other new tests: SplitDesign domain 4/4 PASS; `ADiscountedBillAcceptsEqualSplitAndCustomSplitWithAdjustedTotals` PASS.
