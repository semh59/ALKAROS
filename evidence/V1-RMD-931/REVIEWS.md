# V1-RMD-931 Reviews

## qa-backend
Medium fixed: an untracked product skipped its extras' stock and theoretical consumption; now only the product's own deduction is skipped (test + mutant added). Accepted: an untracked product on the daily menu is not counted in portion counters (no portions to limit); an untracked product with a mapping is tracked (mapping wins); a rare retry after switching an order's product to untracked mid-hold can conflict; the lookup is one shared helper (catalog table read by SQL, same pattern as other Host stores). Noted outside scope: Trendyol Go intake has no stock reservation path at all.

## qa-test-kalitesi
Medium: NFC path has no dedicated test (accepted: it calls the same consumption service covered by the submit test and the wide NFC suite stays green); modifier case added. Low: stronger asserts added (kitchen ticket count); QR/Yemeksepeti asserts stay hold-based (no mapping means no stock rows to compare). M1/M2 re-run on the final code shape as M1b/M2b.
