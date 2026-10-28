# V1-RMD-741 evidence

Fix: `RecordPhysicalCountAsync` verifies inside the transaction that the applied on-hand equals the counted quantity; a movement between reading the balance and applying the delta rolls the count back (`PhysicalCountBalanceGuardFailedException`, 409).

- `green.log`: PhysicalCounts tests on real PostgreSQL (port 55432), 11/11, exit 0.
- `red.log` + `mutations.log`: removing the verification fails `AMovementBetweenReadingAndApplyingRollsTheCountBack`; source restored and verified with `cmp`.
