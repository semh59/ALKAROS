# V1-RMD-742 evidence

Fix: `RebuildAllBalancesAsync` reads per-item/location sums with one database-side aggregate (`GetLedgerBalanceSumsAsync`, no unpaged-row cap) BEFORE wiping the balances table, then writes them. A read failure leaves existing balances intact; a location with more than 5000 movements rebuilds successfully.

- `green.log`: BalanceProjection tests on real PostgreSQL (port 55432), 16/16, exit 0. Other Inventory test projects (fakes updated) pass: `t_*.log`.
- `red1.log`, `red2.log` + `mutations.log`: wiping before aggregating, and a wrong Out sign, are both caught; sources restored and verified with `cmp`.
