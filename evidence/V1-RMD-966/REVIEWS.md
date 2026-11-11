# V1-RMD-966 Reviews

## ui-turkce-metin-uzmani
- Menü is the screen where products, prices and sale status are managed. The till list is empty also when all products are inactive, unavailable, unpriced or out of stock, so "add products" was too narrow -> the sentence now says a manager should check product, price and sale status on the Menü screen.
- A failed catalog load does not reach this message (the till goes back to the login state).

## qa-test-kalitesi
- Medium (loading flash): the catalog area is drawn only after the session is ready and the catalog is set before that, so no empty flash (verified in code).
- Low: search typed then cleared -> asserted (the message goes away and the cards return); spaces-only search -> asserted (M3).
