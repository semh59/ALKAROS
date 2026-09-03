# ALKAROS operational data housekeeping

> Source basis: EXT:POSTGRESQL-18.4, PO:2026-09-01
> Owner task: V1-RMD-091

The `housekeeping` verb deletes **expired ephemeral operational rows** that have
no value after their expiry and would otherwise grow without bound over months of
service. It is a database-hygiene job, not a compliance job.

## What it deletes

| Table | Condition | Why safe to delete |
| --- | --- | --- |
| `idempotency_keys` | `expires_at < now()` | Duplicate-submit guard with a 24 h retention window (V0-ARC-003). Past expiry the stored `response_envelope` (BYTEA) is dead weight. |
| `identity.device_sessions` | `expires_at < now() - grace` **or** `revoked_at < now() - grace` | An expired or long-revoked auth session cannot be used. `token_hash` is already a hash, not a credential. The `identity.session_operations` children cascade (`ON DELETE CASCADE`). |

`grace` defaults to **7 days** (`--grace-days N`), so recently expired/revoked
rows stay briefly visible for debugging.

## What it never touches

- `orders`, `bills`, fiscal receipts, invoices — legal retention (10 years,
  Turkish Tax Procedure Law). Out of scope by design.
- Any personal data subject to KVKK retention/anonymization. That is a separate
  workstream (`V15-KVK-001` retention execution, `V15-KVK-002` cross-store
  anonymization) driven by the approved `V0-CMP-003` KVKK data inventory, where
  retention periods are 5–10 years and disposal is anonymize-not-delete. This
  job does **not** implement any part of it.
- `table_mgmt.table_reservations`, raw provider payloads — separate assessment.

## Running it

On demand / from cron, against the running stack:

```sh
docker compose --profile ops run --rm housekeeping
```

Directly:

```sh
ALKAROS_DB_PASSWORD=... dotnet ALKAROS.Host.dll housekeeping \
  --db-url postgresql://alkaros@postgres:5432/alkaros [--grace-days 7]
```

Output (exit code 0):

```text
housekeeping: idempotency_keys=<n> device_sessions=<n> grace_days=7 seconds=<s>
```

All deletes run in one `READ COMMITTED` transaction; a failure rolls back with a
non-zero exit and no partial deletion.

### Suggested schedule

```cron
# daily at 04:30, outside trading hours
30 4 * * * cd /opt/alkaros && docker compose --profile ops run --rm housekeeping >> /var/log/alkaros-housekeeping.log 2>&1
```

Daily is ample: `idempotency_keys` turns over in 24 h and device sessions expire
on their own timeline; the sweep just reclaims the space.
