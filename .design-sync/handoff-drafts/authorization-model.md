# Authorization Model — differentiated decision record (DRAFT)

> **Proposed task:** V1-IAM-016 (decision) + V1-IAM-017..023 (implementation wave)
> **Status:** DRAFT — awaiting Semih approval
> **Work type:** decision
> **Approver:** Semih — pending
> **Decision type:** Business + architecture decision
> **Supersedes scope of:** the "deferred authorization wave" noted in
> `database/migrations/V1/V1-RMD-097/042-authorization-role-catalog.up.sql`

## 1. Why this exists

Shipped V1 has one coarse application permission (`pos.cashier.mutate`) held by
`cashier` / `supervisor` / `manager`. There is no `waiter` role, so a waiter must
log in as a cashier and — at a cashier station — can do everything a cashier can
(reserve tables, split bills, open the drawer, …). The migration itself flags the
split as deferred.

Toast, Square for Restaurants and Lightspeed all solve role-based access the
same way: **the action is gated, not the terminal; a synchronous manager PIN /
card-swipe authorizes each restricted action; the audit row says "manager X
approved".** That model has four structural weaknesses this decision targets:

| Competitor weakness | ALKAROS answer |
| --- | --- |
| Manager PIN-swipe is a physical bottleneck during a rush | **Asynchronous contextual grant** — request pops on any on-shift manager's device with full context; ~2s approve from where they stand |
| The manager becomes a walking PIN; "approved" audit is theatre | **Policy engine + policy-path in every event** — routine cases auto-approve within a tunable limit and are logged, not interrupted; humans see only exceptions |
| Roles are static; a senior server = a new hire | **Time-boxed delegation** — a manager grants "Ayşe holds `bills.comp` ≤ ₺200 until 22:00", auto-revoked, audited |
| LAN outage forces all-or-nothing (lock the floor or open the vault) | **Bounded offline authority** — the device carries a signed, short-TTL self-approval budget; beyond it, blocked; on reconnect every offline grant is re-validated and queued for review |

Plus one preventive layer no competitor ships:

- **Behavioural tightening** — the authorization service reads a rolling
  per-user rate (void %, comp ₺/shift, discount count). A user whose rate spikes
  ≥ 3× their 30-day baseline is auto-moved to `requires_grant` even on actions
  their role normally auto-approves, until a manager clears it. Audit shifts from
  forensic to preventive.

## 2. Permission vocabulary (replaces `pos.cashier.mutate`)

`pos.cashier.mutate` is retained only as a deprecated alias during migration and
removed in the last wave task. New application permission codes:

| Code | Guards |
| --- | --- |
| `orders.create` | start / edit an unsent order |
| `orders.send` | fire an order / course to the kitchen |
| `tables.status` | Available ↔ Occupied ↔ Cleaning on own-served tables |
| `tables.reserve` | create / cancel / claim a manual reservation |
| `tables.transfer` | transfer an open order/bill between tables |
| `tables.merge` | merge / unmerge tables |
| `floorplan.manage` | edit zones, table layout, capacities |
| `bills.split` | operational bill splitting |
| `bills.void` | void a **sent** item (unsent items need only `orders.create`) |
| `bills.comp` | zero-price a delivered item |
| `bills.discount` | apply a line/bill discount above the preset ladder |
| `cash.drawer` | no-sale / drawer open / count |
| `reports.view` | operational reports |
| `catalog.manage` | menu / price / routing (already exists, unchanged) |

## 3. Roles (seed defaults; tunable from the manager UI)

| Permission | waiter | cashier | supervisor | manager |
| --- | :-: | :-: | :-: | :-: |
| `orders.create` / `orders.send` | ✅ | ✅ | ✅ | ✅ |
| `tables.status` | ✅ | ✅ | ✅ | ✅ |
| `tables.reserve` | ❌ | ✅ | ✅ | ✅ |
| `tables.transfer` / `tables.merge` | ❌ | ✅ | ✅ | ✅ |
| `floorplan.manage` | ❌ | ❌ | ✅ | ✅ |
| `bills.split` | ❌ | ✅ | ✅ | ✅ |
| `bills.void` | grant | grant | ✅ | ✅ |
| `bills.comp` | grant | grant | ✅ | ✅ |
| `bills.discount` | grant | grant (ladder only ✅) | ✅ | ✅ |
| `cash.drawer` | ❌ | ✅ | ✅ | ✅ |
| `reports.view` | ❌ | ❌ | ✅ | ✅ |
| `catalog.manage` | ❌ | ❌ | ❌ | ✅ |

`grant` = the role does not hold the permission outright; the action raises an
authorization request resolved by §4. `supervisor` = shift lead / şef garson.

**Open product questions for Semih:**

1. Can a waiter void/comp on **their own** check via a grant, or never?
2. Does `cashier` get a standing `bills.discount` ladder (e.g. ≤ 10 %) without a grant?
3. Is `supervisor` a floor role (şef garson) or a junior-manager role?

## 4. Grant flow (`authorization_grants`)

A `grant` action creates an immutable request:

```text
requester_user_id, permission_code, context (table, order, bill, amount,
reason_code from the fixed catalog), requester_rate_snapshot, created_at
```

Resolution, in order:

1. **Policy auto-approve** — `authorization_policies` row for
   `(permission_code, role)` of shape `auto_within(limit_amount, max_count,
   window)`. Within limit and count → `granted` with `policy_path = 'auto'`,
   no human notified. Beyond → step 2.
2. **Delegation** — an active `authorization_delegations` row
   (`grantee_user_id, permission_code, limit_amount, expires_at`) covers it →
   `granted`, `policy_path = 'delegation'`, delegator recorded.
3. **Manager decision** — request is pushed to every on-shift `manager` /
   `supervisor` device with the full context block; first responder's
   approve/deny wins → `granted` / `denied`, `policy_path = 'manual'`,
   `approver_user_id` recorded.

Every terminal state is one append-only row carrying requester, approver (or
policy), `policy_path`, `reason_code`, and the monetary delta, so
`reporting.*` gets "comp ₺ by reason by server by day" with no extra plumbing.
Reuses `DenialEvent` sink shape for denies.

## 5. Bounded offline authority

At session start the Host issues the device a signed **offline authority
budget** (JWT-style, short TTL, per session):

```text
{ user, session, budget: { "bills.comp": {amount: 15000, count: 2},
                            "bills.void": {count: 1} }, exp: session_start + 4h }
```

Offline:

- Permissions the role holds outright → work, queued as today.
- `grant` actions → allowed only while the signed budget has headroom;
  each consumes budget locally. Beyond budget or expired → blocked with a
  "reconnect required" state.
- On reconnect, every offline-authorized action is **re-validated server-side**
  against the live policy and flagged `offline_pending_review` for a manager.

This is the "asymmetric offline resilience" principle (DESIGN.md §1) applied to
authorization: bounded, known, per-device trust offline; full reconciliation
online. The fraud window is a configured number, never "everything".

## 6. Enforcement points

Every Experience endpoint that currently calls
`RequireMutationAsync` / `MutationPermission` is re-pointed to its specific code
(Tables, Orders, Billing, Cash). `AllowedCommands(table)` is computed from the
caller's held permissions **plus** which `grant` actions are reachable, so the
POS client can show "Void (needs approval)" instead of hiding it.

## 7. Implementation wave (proposed task cluster)

| Task | Scope |
| --- | --- |
| `V1-IAM-016` | **This decision record** → `docs/domain/authorization-model.md` |
| `V1-IAM-017` | Permission-code catalog split + `pos.cashier.mutate` alias; migration reseed of `role_permissions`; `waiter` role; `AuthorizationService` unchanged |
| `V1-IAM-018` | `authorization_policies` table + evaluation (`auto_within`); manager UI to edit limits |
| `V1-IAM-019` | `authorization_grants` request/resolve engine + append-only event + `reporting.*` projection |
| `V1-IAM-020` | Manager push + approve/deny surface (PosTerminal + WaiterPwa manager view) |
| `V1-IAM-021` | `authorization_delegations` (time-boxed) + auto-revoke job |
| `V1-IAM-022` | Bounded offline authority budget: issue at session start, local spend, reconnect re-validation + `offline_pending_review` |
| `V1-IAM-023` | Behavioural tightening: rolling rate snapshot + auto `requires_grant` + manager clear |
| `V1-IAM-024` | Re-point every Experience endpoint; remove `pos.cashier.mutate`; `AllowedCommands` = held ∪ reachable-grants |

`V1-RMD-100` (reservation-policy doc reconciliation) stays as-is and is
superseded in effect once `V1-IAM-017` gives `tables.reserve` a real gate.

## 8. Invariants

- No action is authorized without exactly one audit row (grant, delegation, or
  role-held check + endpoint log).
- A session's effective authority never increases mid-request; a grant
  authorizes exactly one command instance, identified by an idempotency key.
- Offline authority is bounded by the signed budget; it cannot be replayed
  (session-scoped `exp` + server-side single-use reconciliation).
- Removing a role or permission takes effect on the next request
  (`AuthorizationService` reads live, no session cache).
