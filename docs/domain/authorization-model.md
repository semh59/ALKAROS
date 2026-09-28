# Authorization Model — approved decision record

> **Task:** V1-IAM-016
> **Status:** Done
> **Work type:** decision
> **Access date:** 2026-09-04
> **Approver:** Semih — 2026-09-04
> **Decision type:** Business + architecture decision
> **Source basis:** PDF:II.2.1, PDF:III.3; PO:2026-09-04
> **Supersedes scope of:** the deferred authorization wave noted in
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
| Roles are static; a senior server = a new hire | **Time-boxed delegation** — a manager grants "Ayşe holds `bills.comp` ≤ ₺200 until 22:00"; it stops applying the moment it expires (query-filter enforced) or a manager cancels it early, either way audited |
| LAN outage forces all-or-nothing (lock the floor or open the vault) | **Bounded offline authority** — the device carries a server-held, short-TTL self-approval budget; beyond it, blocked; on reconnect every offline grant is re-validated and queued for review |

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
| `payments.take` | take a payment on a bill (card/EFT tender; a cash tender also needs `cash.drawer`) — V1-RMD-401 |
| `reports.view` | operational reports |
| `catalog.manage` | menu / price / routing (already exists, unchanged) |
| `kitchen.advance` | advance a kitchen ticket/item one stage forward (Queued→Preparing→Ready→Served) — split out of `orders.send` (V1-IAM-028) so the narrow `kitchen-staff` role can hold it without also being able to cancel a ticket. A ticket/item transition to `Cancelled` still requires `orders.send` regardless of which role/permission gate is checked; `KitchenOperationsEndpoints.TicketTransitionPermission` branches on the target state. |

## 3. Roles (seed defaults; tunable from the manager UI)

| Permission | waiter | cashier | supervisor | manager |
| --- | :-: | :-: | :-: | :-: |
| `orders.create` / `orders.send` | ✅ | ✅ | ✅ | ✅ |
| `tables.status` | ✅ | ✅ | ✅ | ✅ |
| `tables.reserve` | ❌ | ✅ | ✅ | ✅ |
| `tables.transfer` / `tables.merge` | ❌ | ✅ | ✅ | ✅ |
| `floorplan.manage` | ❌ | ❌ | ✅ | ✅ |
| `bills.split` | ❌ | ✅ | ✅ | ✅ |
| `bills.void` | grant (own check) | grant | ✅ | ✅ |
| `bills.comp` | grant (own check) | grant | ✅ | ✅ |
| `bills.discount` | grant | grant | ✅ | ✅ |
| `cash.drawer` | ❌ | ✅ | ✅ | ✅ |
| `payments.take` | ❌ (configurable) | ✅ | ✅ | ✅ |
| `reports.view` | ❌ | ❌ | ✅ | ✅ |
| `catalog.manage` | ❌ | ❌ | ❌ | ✅ |

`grant` = the role does not hold the permission outright; the action raises an
authorization request resolved by §4.

**Resolved (Semih, 2026-09-04):**

1. A waiter **may** void/comp, but only on **their own** check and only through a
   grant (`context.requester_user_id` must equal the order's serving user; a
   grant on another server's check is auto-denied before it reaches a manager).
   An unassigned check (no serving user) is not the waiter's own check either and is auto-denied the same way
   (Semih, 2026-09-28, V1-RMD-402).
2. There is **no standing discount ladder** — every `bills.discount`, including
   from a `cashier`, is a grant. Only `supervisor` / `manager` hold it outright.
3. `supervisor` is the **floor role (şef garson)**: a senior waiter who holds
   `bills.void` / `bills.comp` / `bills.discount` / `floorplan.manage` /
   `reports.view` outright so the rush keeps moving without a manager, but never
   `catalog.manage` and never staff/finance settings.

**Resolved (Semih, 2026-09-28, V1-RMD-401):** who may take a payment is a per-business setting, not a fixed
role rule. `payments.take` is seeded to `cashier` / `supervisor` / `manager`; a business that hands its waiters a
card terminal grants it to the `waiter` role through role management, with no code change.

### 3.1 `kitchen.advance` and the `kitchen-staff` role (V1-IAM-028)

All four roles above (waiter/cashier/supervisor/manager) also hold
`kitchen.advance` outright — additive, no regression, since they already held
`orders.send` and could already advance a kitchen ticket before the split.
A fifth, kitchen-only role, `kitchen-staff` ("Mutfak Personeli", a line cook
with no FOH access), holds **only** `kitchen.advance` — it can move a ticket/
item forward but cannot cancel it, approve a reprint (`kitchen.reprint`),
manage printer routing (`kitchen.routing.manage`), or suspend a sold-out
product (`kitchen.availability.suspend`, V1-IAM-029's "Mutfak Şefi" role).
Note that `kitchen.reprint` and `kitchen.routing.manage` are Kitchen-module
local permission constants (defined in `KitchenOperationsEndpoints.cs`, not
in `ApplicationPermissions.Codes`) — `kitchen.advance` is the first Kitchen
permission added to the central catalog, because it needed to be granted to
the four existing FOH roles too, not just a new Kitchen-only one.

### 3.2 `kitchen-chef` role (V1-IAM-029, extended V1-IAM-030)

A sixth role, `kitchen-chef` ("Mutfak Şefi", an executive chef — distinct
from the FOH `supervisor`/"şef garson", a senior waiter role with no kitchen
duties). Holds everything `kitchen-staff` cannot: `orders.send` (cancel a
kitchen ticket/item, or report a problem — the same permission a `Cancelled`
transition already requires from any role), `kitchen.reprint` (approve or
reject a physical-print recovery reprint), `kitchen.availability.suspend`
(86 a sold-out product from the Kitchen screen, V1-KIT-008), and
`kitchen.routing.manage` (manage printer routing, V1-IAM-030 — V1-IAM-029
had left this open; Semih's decision, 2026-09-14, granted it). `kitchen.advance`
is granted too, so the chef is not left unable to do what a line cook
already can.

Like `kitchen-staff`, none of `kitchen-chef`'s grants are listed in
`ApplicationPermissions.RoleGrants` (that dictionary only covers the four
original FOH roles) — seeded directly by migrations 111 (the role itself
plus `orders.send`/`kitchen.advance`/`kitchen.reprint`/
`kitchen.availability.suspend`) and 112 (`kitchen.routing.manage`).

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

At session start the Host issues the device a server-held **offline authority
budget** row (short TTL, per session, keyed by an unguessable `budget_id` —
not a signed token; see the V1-IAM-022 task's "Tasarım sapması" note):

```text
{ budget_id, user, session, budget: { "bills.comp": {amount: 15000, count: 2},
                                       "bills.void": {count: 1} }, exp: session_start + 4h }
```

Offline:

- Permissions the role holds outright → work, queued as today.
- `grant` actions → allowed only while the budget has headroom;
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
| `V1-IAM-021` | `authorization_delegations` (time-boxed); expiry enforced by the read-path query filter, `revoked_at` for an early manager cancel |
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
- Offline authority is bounded by the server-held budget row; it cannot be
  replayed (session-scoped `exp` + server-side single-use reconciliation).
- Removing a role or permission takes effect on the next request
  (`AuthorizationService` reads live, no session cache).
