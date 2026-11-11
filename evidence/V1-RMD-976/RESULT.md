# V1-RMD-976 Result

Status: Done.

- Root cause of "Siparişe devam et" 409 and "Hesabı al" with an empty bill number: the table pointer rebuild wrote the all-zero id (List<Guid>.FirstOrDefault() on an empty list) as the bill pointer of a table with an open order and no bill (and as the order pointer for the reverse). A table with a nil bill pointer reads as having an active bill, so starting an order is refused and the bill screen opens `billId=0000...`. The earlier fix (921) did not run the rebuild, so its test never saw this; M-10 and M-11 worked only because the rebuild had not reached them yet.
- Fix: the rebuild writes NULL when there is no order or no bill. Migration 228 clears nil pointers already stored (the live M-01, M-02, M-03) and refuses them from now on.
- Tests: pointer projector 14/14, migration/manifest 18/18; 3 mutants killed. A separate red run is in red.log (2 tests).
- Still open (next task): after the fix, "Siparişe devam et" on a table whose order was already sent may still be refused because the sent order is not editable; to be verified in a real browser.
