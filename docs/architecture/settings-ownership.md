# Settings Ownership — approved decision record

> **Task:** V0-ARC-005
> **Status:** Done
> **Work type:** decision
> **Source basis:** PDF:III.27
> **Access date:** 2026-08-02
> **Approver:** Semih — 2026-08-03
> **Decision type:** Business decision (named business approver)

PDF `III.27.1` defines `settings.settings`: `setting_key unique`,
`setting_value`, `data_type`, `scope`, `active`, `updated_at`; `III.27.2`
defines `setting_history` with `old_value`, `new_value`, `changed_by`,
`changed_at`. The PDF does not classify which values may live in settings —
that classification is bound below.

## Selected decisions

| Rule | Selected result | Basis |
| --- | --- | --- |
| Module owner | Every setting has exactly one module owner named in the settings registry; cross-module reads go through that owner | Single-ownership principle (module boundaries) |
| Scope | `scope` is one of `Global`, `Module`, `Device`, `Tenant`; a setting's scope is fixed at registration | PDF `III.27.1 scope` |
| Validation | Every setting carries a `data_type`-matched validation; invalid writes are rejected, never stored | PDF `III.27.1 data_type` |
| History | Every setting change writes a `setting_history` row with `changed_by` and `changed_at` | PDF `III.27.2`; audit-first (III.1.7) |
| Secret storage ban | Credentials, tokens, passwords and provider secrets are never settings; they live in the secret store only (V0-ARC-005 handoff `V15-SEC-001`; `II.11` "Sensitive provider credentials are never embedded in code") | PDF `II.11` Security rules |
| Restart requirements | Settings that require restart are flagged at registration; no value change is silently unapplied | Operation clarity |
| Deprecation | Deactivated settings (`active = false`) keep history and are never physically deleted | PDF `III.27.2` immutability |

## Rejected alternatives

- Secrets in settings with "masked" values — rejected: masking is not
  storage; PDF II.11 bans embedded credentials.
- Settings without module owner — rejected: unowned settings have no
  validation/history contract.
- Physical deletion of deactivated settings — rejected: history must remain
  (audit-first).

## Invariants (consumers V1-SET-001, V15-SEC-001)

- Every known setting has owner, type, scope, default, validation and
  history; credentials are explicitly excluded from generic settings.
- A setting value can always be traced to the `setting_history` row that
  changed it.

## Known settings as built

The registry is the code, not this file. Owners and edit limits below match
what the Host registers and the server enforces on write (reasons are at most
500 characters; a change needs the `settings.manage` permission and writes a
history row naming who changed it).

| Key | Module owner | Type | Default | Allowed values |
| --- | --- | --- | --- | --- |
| `business.name` | settings | Text | empty | up to 80 characters, no control characters |
| `business.accent_theme` | settings | Text | palette default | a key of the accent palette |
| `waiter.max_active_tables` | orders | WholeNumber | 0 | 0–50, 0 = unlimited |
| `kitchen.dense_mode_threshold` | kitchen | WholeNumber | 9 | 1–99 |
| `kitchen.live_sync_enabled` | kitchen | Toggle | false | true/false |
| `qr_ordering.pending_confirmation_timeout` | qr_ordering | Duration | 5 min | 1 min–24 h |
| `reservations.dedicated_station_enabled` | tables | Toggle | false | true/false |
| `garson.*_enabled` (nine feature switches) | waiter | Toggle | true | true/false |

Reading rules: a missing, deactivated or unreadable value falls back to the
default. History lists show the newest 5000 changes, newest first, and ties on
the same instant are ordered by history id so paging is stable.
The database enforces this since migration 197: a trigger refuses deleting a history row and changing anything in it
except clearing `changed_by` when that user is removed, and a setting that still has history cannot be deleted.
