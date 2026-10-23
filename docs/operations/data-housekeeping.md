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

| `customer_display.display_sessions` | same as `identity.device_sessions` | A second-screen session that expired or was revoked cannot be used again. |
| `customer_display.pairing_requests` | `expires_at < now() - grace` **or** `consumed_at < now() - grace` | A pairing code past its life or already used is dead. |
| `notifications.serving_handoff_notes` | `created_at < now() - notification-days` | A hand-off note is read once; an unread one loses its meaning after weeks. |
| `notifications.push_subscriptions` | not used for `notification-days`, or the user is inactive | A device that has not received a successful push for months, or belongs to a deactivated person, is not reachable in any useful way. |
| `notifications.help_requests` | `created_at < now() - help-request-days` | The record of who called for help; kept a year by default. |

`grace` defaults to **7 days** (`--grace-days N`), so recently expired/revoked
rows stay briefly visible for debugging. `--notification-days` defaults to **90** and
`--help-request-days` to **365**; they are proposed values until the Product Owner decides
the retention lengths, and can be changed per run.

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
docker compose -f compose.yaml -f compose.ops.yaml run --rm housekeeping
```

Directly:

```sh
ALKAROS_DB_PASSWORD=... dotnet ALKAROS.Host.dll housekeeping \
  --db-url postgresql://alkaros@postgres:5432/alkaros [--grace-days 7] [--notification-days 90] [--help-request-days 365]
```

Output (exit code 0):

```text
housekeeping: idempotency_keys=<n> device_sessions=<n> display_sessions=<n> display_pairings=<n> handoff_notes=<n> push_subscriptions=<n> help_requests=<n> grace_days=7 notification_days=90 help_request_days=365 seconds=<s>
```

Deletes run in batches of 5000 rows, each batch committing on its own, so a large
backlog never holds one long lock. A failure stops with a non-zero exit after the batches that
already finished; running the verb again continues where it stopped.

### Suggested schedule

```cron
# daily at 04:30, outside trading hours
30 4 * * * cd /opt/alkaros && docker compose -f compose.yaml -f compose.ops.yaml run --rm housekeeping >> /var/log/alkaros-housekeeping.log 2>&1
```

Daily is ample: `idempotency_keys` turns over in 24 h and device sessions expire
on their own timeline; the sweep just reclaims the space.
