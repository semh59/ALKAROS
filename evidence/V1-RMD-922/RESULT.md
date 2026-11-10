# V1-RMD-922 result

Report finding: no collection entry on the sales screen after "Siparişi gönder"; the pending-checks card (the only way to the payment page) showed no sign that it is tappable; no way to collect a table's check from the table panel.
Done: a sent order shows a primary "Tahsil et" (opens or finds the bill, goes to the payment page; without the bill-opening permission the sentence "Hesap açmak için bölme yetkisi gerekir." is shown instead); pending-checks cards show "Tahsil et ›"; a table with an order or bill shows "Hesabı al" (table panel became the tested TableOrderBridge component). One helper (collectionHrefForOrder) serves the three; the server's from-order opening is idempotent (returns the active bill), so repeated taps do not create a second bill.
Red (observed before the wiring): Cashier.collect tests 2 failed, no button / no sentence. Green: whole PosTerminal suite 1154/1154, tsc clean. 5 mutants killed (mutations.txt).
Not verified in a real browser here (layout of the new card line, page jump); the payment page itself is untouched.
