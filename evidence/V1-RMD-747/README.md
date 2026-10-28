# V1-RMD-747 evidence

Fix: `RebuildReservationBalancesAsync` now replaces the applied-event log in one transaction from `portion_reservations` (a Reserved event for every reservation, a Terminal event for each finished one) via `RebuildAppliedEventsFromReservationsAsync`. Before, the log was only deleted, so replaying the terminal transition of an already released/consumed reservation after a rebuild was applied a second time. The unused `ResetAppliedEventsAsync`/`RecordAppliedEventsForRebuildAsync` pair was removed.

- `red.log`: before the fix the replay was not idempotent (Expected IsIdempotentReplay true, found False).
- `green.log`: ReservationBalanceProjection tests on real PostgreSQL (port 55432), 14/14, exit 0. PortionReservations projects (fakes updated): `t_*.log`, all green.
- `red2.log` + `mutations.log`: without the Terminal events the test fails again; source restored and verified with `cmp`.
