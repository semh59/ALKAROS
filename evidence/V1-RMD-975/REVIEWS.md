# V1-RMD-975 Reviews

- qa-backend: no critical; findings fixed: four-decimal and upper bound on quantity, reserved stock cannot be transferred away, replay answered before activity checks, transfer legs not reversible alone; replay balances documented. Opposite-direction concurrency and 403 role tests not added (lock order is by id; manager permission is the same filter as the other inventory writes).
- qa-veritabani: no critical; down migration warning added (ledger is immutable, rollback only before the first transfer).
