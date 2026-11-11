# V1-RMD-947 Result

Status: Done (no code defect; the lock is the browser's own modal print dialog).

- The page clears its busy state, fills the sheet and redraws before opening the dialog, and a second press while the receipt is read sends one request and one dialog; both are locked by tests (3 mutants killed).
- Product note for Semih: silent printing on the till needs the browser started with its silent-print flag (deployment setting, not in the repo).
