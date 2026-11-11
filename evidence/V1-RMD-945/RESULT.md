# V1-RMD-945 Result

Status: Done (claim not reproducible on the server).

- No defect found on the server: the balance is written by a trigger in the ledger insert, the list and the statement read it, and a regression assertion now locks list, search and an untouched customer (green on the first run, 2 mutants killed).
- Likely cause of the trial's zero: a card left open from before the charge. Follow-up V1-RMD-946 refreshes the card when the page is shown again.
