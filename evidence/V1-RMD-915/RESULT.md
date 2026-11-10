# V1-RMD-915 result

The till's queue of checks awaiting payment now shows paid amount = allocations minus refunds of that bill (the collected definition of V1-RMD-911). Before, a refunded payment still counted as paid, so the cashier read the remaining debt wrongly.

- Test: TheTillQueueShowsWhatIsPaidAfterTheRefundsAreTakenOff (100 paid, 30 refunded -> 70; another bill's refund does not leak in). Red before (red.log: expected 70, actual 100), TableDraft project 128/128 and the Billing MergedBillOrderLinkTests 14/14 after. The TableDraft test project now applies migration 182 (payment_reversals).
- Mutations: 3 killed (mutations.txt).
