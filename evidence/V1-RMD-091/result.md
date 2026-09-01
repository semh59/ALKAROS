# V1-RMD-091 - Operational data housekeeping sweep

- Date: 2026-09-01
- Owner: claude-code-019uHsMymyuC1tZ5DHtmWsRi

## Gap

`idempotency_keys` (a BYTEA `response_envelope` per write, 24 h retention per
V0-ARC-003) and `identity.device_sessions` (expired + revoked auth sessions, plus
their `identity.session_operations` children) grew without bound:
`IdempotencyKeyStore.SweepExpiredAsync` existed but had **zero callers**, device
sessions had **no cleanup path at all**, and the Host had no periodic-maintenance
infrastructure.

## Change

`src/Host/Program.cs` — new `housekeeping` verb:

```
housekeeping --db-url <url> [--grace-days <N>]   (default grace 7 days)
```

One `READ COMMITTED` transaction:

```sql
DELETE FROM idempotency_keys WHERE expires_at < now();
DELETE FROM identity.device_sessions
 WHERE expires_at < now() - make_interval(days => @grace)
    OR (revoked_at IS NOT NULL AND revoked_at < now() - make_interval(days => @grace));
```

`identity.session_operations` rows cascade with their parent session.
Password from `ALKAROS_DB_PASSWORD`; the URL must not carry a password (same
guard as `provision-manager`). Output:

```
housekeeping: idempotency_keys=<n> device_sessions=<n> grace_days=<g> seconds=<s>
```

Exit 0 on success; `HostExitCode.StartupFailed` (2) on bad args or DB error.

`compose.yaml` — `housekeeping` service under the `ops` profile (runtime image).
`docs/operations/data-housekeeping.md` — what it sweeps, why safe, cron schedule,
and an explicit statement that KVKK personal-data retention is a separate
workstream (`V15-KVK-001` / `V15-KVK-002`, 5–10 year periods, anonymize-not-delete)
and that this job never touches orders/bills/fiscal/invoice data.

## Tests

`tests/Host/MigrationComposition/Program/HousekeepingTests.cs` — real test DB:

- `SweepDeletesExpiredRowsAndKeepsFreshAndInGraceRows`: 1 expired idempotency key
  deleted / 1 valid kept; of 5 sessions (expired-beyond-grace,
  expired-in-grace, valid, revoked-beyond-grace, revoked-in-grace) exactly the
  2 beyond-grace are deleted; output reports `idempotency_keys=1 device_sessions=2`.
- `ZeroGraceDaysSweepsRecentlyExpiredSessions`: `--grace-days 0` deletes a
  session that expired 1 day ago.
- `MissingDbUrlFailsClosed`, `NegativeGraceDaysFailsClosed`: exit 2.

Result (Docker `alkaros-sdk10-rt8` + `alkaros-pg`):

```
dotnet test tests/Host/MigrationComposition --filter FullyQualifiedName~Housekeeping
Passed!  - Failed: 0, Passed: 4, Skipped: 0, Total: 4
```

## Scope

Operational hygiene only. KVKK personal-data retention/anonymization is
`V15-KVK-001` / `V15-KVK-002` per the approved `V0-CMP-003` inventory and is not
touched here.
