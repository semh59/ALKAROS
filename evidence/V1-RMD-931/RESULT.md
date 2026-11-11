# V1-RMD-931 Result

Product form now offers "Stok takibi yok". A product with that mode and no stock mapping sells in every channel (cashier/waiter submit, QR accept, NFC, Yemeksepeti intake) with no stock deduction, reservation or missing-mapping refusal. Tracked products behave as before; an untracked product that has a mapping is still deducted; extras with their own stock are still deducted.
Red: new client and host tests failed. Green: PosTerminal 108 files / 1156 tests; TableDraft 130, Confirmation 40, OnlineOrdering 203 (after fixes), Nfc 29, VoidSent 35, Void 5, CrossChannelReservation 39+. Mutants: 10 killed (mutations.txt), files restored (cmp ok).
Pre-existing, unrelated: the architecture boundary tests fail the same way without this change (CustomerAging and Authorization edges).
Existing "unmapped product is refused" tests now seed a stock-tracked product.
