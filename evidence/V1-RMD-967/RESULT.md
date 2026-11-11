# V1-RMD-967 Result

Status: Done.

- New PUT stock-items/{id}/default-location (set or clear) and a "Konumu değiştir" action on the stock screen. An unknown item or location answers 404, an inactive location 409, and an item that already holds stock answers 409 STOCK_LOCATION_LOCKED ("Bu kalemin stoku var; varsayılan konumu değiştirilemez."), because its balances stay where they were booked.
- Tests: StockMaster module 22/22 (domain and real PG), Host inventory 40/40, stock screen 16/16; 13 mutants killed (mutations.txt). The first red run was a compile failure (tests were written before the method existed); the stock-lock test has its own red log.
- Found, not part of this task: the ApiConventions test EveryMutatingEndpointTakesAnIdempotencyKey fails for POST .../receipt/original-reset (V1-RMD-901); the cashier ticket title code has a type error (orderNumber can be null) from V1-RMD-962.
- Open product note: there is no stock transfer between locations, so a wrongly placed item that already holds stock cannot be moved.
