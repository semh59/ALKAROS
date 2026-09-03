# Table Reservation Policy — approved decision record

> **Task:** V0-DOM-005
> **Status:** Done
> **Work type:** decision
> **Source basis:** PDF:II.2.3, PDF:II.3.16, PDF:II.5.15, PDF:III.5, CORR:C5
> **Access date:** 2026-08-02
> **Approver:** Semih — 2026-08-03
> **Decision type:** Business decision (named business approver)

PDF `II.5.15` defines table `current_status`: `Available, Occupied,
Reserved, Cleaning, OutOfService` and states the table state machine is
deliberately lighter than financial machines (no ReconciliationCase, no
mandatory actor/reason), subject to the general concurrency rule (I.14,
optimistic concurrency). `CORR:C5`'s fix (`I.35`/`II.5.15`) binds the QR
seating race: `PendingConfirmation` moves the table to `Reserved`,
`Accepted` to `Occupied`, `Rejected` back to `Available`.

## Selected decisions

| Rule | Selected result | Basis |
| --- | --- | --- |
| `Reserved` meaning | The table is not physically occupied but not free for other seating; it is owned by exactly one owner — a pending QR order, or one active manual reservation record (`table_mgmt.table_reservations`, `status = 'Active'`) | PDF `II.5.15` + CORR:C5 fix; Amendment 2026-09-03 |
| Who creates `Reserved` | (a) the QR order state machine — entering `PendingConfirmation` moves the table `Available → Reserved`; (b) a cashier-authorized terminal — the POS/Cashier client's manual reservation action (`POST .../table-management/{terminalId}/reservations`), gated by the `pos.cashier.mutate` permission and a valid cashier session. The WaiterPwa has no reservation action; the customer display is forbidden (`403`). | CORR:C5 fix; Amendment 2026-09-03 (Semih) |
| Exit transitions | `PendingConfirmation → Accepted` ⇒ `Reserved → Occupied`; `Rejected`/`Cancelled` ⇒ `Reserved → Available` (subject to optimistic concurrency — if another process changed the table, its change wins) | CORR:C5 fix; I.14 |
| Expiry | No time-based expiry: `Reserved` persists as long as the owning order is `PendingConfirmation`; the order state machine is the single owner | Avoids a second timer state the PDF does not define |
| Walk-in priority | A `Reserved` table is never assigned to a walk-in; walk-in allocation considers only `Available` tables | "not free for other seating" (CORR:C5) |
| Transfer/merge | `Reserved` tables cannot be transferred or merged; transfer/merge apply to `Occupied` tables and carry the soft cache pointers | PDF `III.5.3/III.5.4`; keeping Reserved single-owner |
| Concurrency | All table transitions use `row_version` optimistic concurrency; stale updates are rejected | PDF `II.5.15`/I.14 |
| Cache pointers | `tables.current_order_id`/`current_bill_id` are soft cache pointers; ownership truth is `orders.orders.table_id`/`billing.bills.table_id` | PDF `III.5.2` note |

## Rejected alternatives

- Time-based reservation expiry — rejected: creates a second ownership
  model; the order state machine already owns the lifecycle.
- Reservations from the WaiterPwa or any customer-facing channel — rejected:
  reservation intake is a cashier-station responsibility; the waiter takes
  orders for seated guests and the QR channel drives its own
  `PendingConfirmation → Reserved` mapping.
- Transferring a `Reserved` table — rejected: breaks the single-owner
  invariant during the pending window.
- Walk-in taking a `Reserved` table on no-show — rejected: no no-show model
  exists; table is released only through the order lifecycle.

## Invariants (consumers)

- `V1-TBL-004`, `V14-QRO-002`: `Reserved` has one persistent owner (the
  pending order or the active manual reservation record), one exit path per
  result, and no time-based expiry.
- The QR flow never invents reservation semantics beyond the
  `PendingConfirmation` mapping; the only non-QR creator is the cashier-gated
  manual reservation action (Amendment 2026-09-03). The WaiterPwa and
  customer display never create `Reserved`.
- A table's `current_status` always converges to its owner's state within the
  same transaction that moves the owner.

## Amendment

- **Date:** 2026-09-03
- **Approver:** Semih (named business approver)
- **Change:** The original record rejected "personnel-created manual
  reservations" and put an explicit reservation UI out of scope. Shipped V1
  already delivers a cashier-gated manual reservation action
  (`POST .../table-management/{terminalId}/reservations`, `pos.cashier.mutate`)
  in the POS/Cashier client; the WaiterPwa deliberately has none. This
  amendment reconciles the record with that behaviour: manual reservation
  intake by a cashier-authorized terminal is an accepted V1 creator of
  `Reserved`, alongside the QR order state machine.
- **Rationale:** Taking phone and walk-up reservations is a standard cashier
  responsibility for a restaurant POS; the constraint that mattered — one
  owner per `Reserved` table, no second timer model — is unchanged, because
  the manual reservation record is itself the single owner.
- **Affected tasks:** `V1-TBL-004` (scope text reconciled), `V1-RMD-013`
  (Host `POST /reservations` surface), `V14-QRO-003` (QR reservation path,
  still deferred).
- **Not changed:** no `waiter` IAM role is introduced and `pos.cashier.mutate`
  is not split; a finer permission split remains deferred work noted in
  `V1-RMD-097`.
