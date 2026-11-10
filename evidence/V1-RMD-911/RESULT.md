# V1-RMD-911 result

Item comp, bill discount (pre-check and apply) and adjustment undo now count what a bill holds with one shared helper, `BillMoneyGuard.ReadCollectedAsync` (allocations minus reversals). Before, only the comp path netted refunds, so after a cash refund a valid discount was refused as "below collected" and a tip could not be taken off.

- Tests: CollectedNettingHttpTests (6: discount after full refund applies; discount still refused below the remainder and accepted at it; manager-needing request not refused; tip undo after refund). Red before (red.log), green after (green.log); BillingSplitHttpTests + TotalCap 89/89; Orders.Comp 38/38.
- Mutations: 7 killed (mutations.txt).
- Unchanged: a discount or comp on a bill with an item refund stays refused (V1-RMD-909).
