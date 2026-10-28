# V1-RMD-745 evidence

Fix: when the refresh statement of `AuthenticateDisplayAsync` returns no row, a second statement re-checks that the session is still valid (not revoked, not expired). A request that waited on a concurrent refresh's row lock no longer gets a false 401.

- `red.log`: before the fix, the new test fails (Expected OK / Actual Unauthorized).
- `green.log`: all DualScreen host tests on real PostgreSQL (port 55432), 138/138, exit 0.
- `red2.log` + `mutations.log`: with the re-check query disabled the test fails again; source restored and verified with `cmp`.
