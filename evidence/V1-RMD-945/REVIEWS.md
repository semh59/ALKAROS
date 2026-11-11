# V1-RMD-945 Reviews

## qa-bulgu-dogrulayici (verification of the disproof)
The server side holds: the only writer of the ledger is PostgresAccountTransactionLedger and the balance trigger runs in the same transaction; the reconcile job never zeroes a debt; retention only picks zero balances; the card reads the single-customer statement, not the limited list. Open point: the page reads the statement only on open and after its own actions, so a card left open can show an old zero -> new task V1-RMD-946 (refresh when the page becomes visible again).

## qa-test-kalitesi
Added: a second untouched customer, the plain list and the search both asserted (balance, limit, available credit), a join mutant (M2) that shifts a balance to another row. Left out: balance above the limit (Math.Max) and receipt then list, covered by the existing statement tests.
