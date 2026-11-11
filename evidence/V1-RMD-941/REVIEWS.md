# V1-RMD-941 Reviews

## qa-frontend
Low: the dictionary holds one sentence per code, while the server words UNSUPPORTED_BILL_STATE per action (split, discount/tip, undo, cancel). The merge and from-order calls of this client never raise those exceptions (read in BillingSplitStore.cs), and PosTerminal calls no discount, tip, undo or cancel endpoint, so the split sentence is the only true one today. If such calls are ever wired to describeSplitFailure they need the server sentence.

## qa-test-kalitesi
Sufficient: exact sentence in billingApi.test.ts and in the workspace test. Low gaps (status variety, a second mutant) accepted: the code ignores the status, and the single mutant removes the sentence entirely.
