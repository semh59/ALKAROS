# V1-RMD-994 Result

A table order is no longer mixed into the quick-sale cart.
- Server: the cashier's order snapshot carries the table number (only the till view, not the customer display snapshot).
- Till: a table order is the cart only when the cashier opened it from the tables screen ("Siparişe devam et" / "Masada sipariş aç" mark it); coming back any other way, the till starts clean and a product tap opens a new quick order. While a table order is the cart, the header says "MASA M-10" and "Masa M-10 · Sipariş 08:25", the message after sending says "Sipariş mutfağa gönderildi.", and a "Hızlı satışa geç" button leaves it with one tap.
- A quick sale refused because a table draft is open now says "Masa M-10 için açık bir sipariş var; Masalar ekranından devam edin." instead of writing the product into that table's order.

- red.log, client-red*.log: red before; green.log 47 of 47 server tests; client-green.log and client-all.log: PosTerminal suite 1221 of 1221; tsc clean; oxlint --max-warnings=0 exit 0; vite build exit 0 (build.log).
- mutations.txt: 7 mutations killed, files restored.
