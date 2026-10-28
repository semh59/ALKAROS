# V1-RMD-743 evidence

Fix: removing a user from a role, or `roles.manage` from a role, now counts remaining active holders and deletes in one transaction under `pg_advisory_xact_lock` (`TryRevokeUserKeepingHolderAsync`, `TryRevokePermissionKeepingHolderAsync`). Two concurrent mutual revokes can no longer both pass the check. The unused `CountActiveHoldersAsync` was removed.

- `green.log`: Identity.Authorization tests on real PostgreSQL (port 55432), 248/248, exit 0.
- `red1-3.log` + `mutations.log`: with the lock disabled the race test fails in all 3 runs (10 iterations each); source restored and verified with `cmp`.
