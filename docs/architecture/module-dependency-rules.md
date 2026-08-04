# Module Dependency Rules — approved decision record

> **Task:** V0-ARC-001
> **Status:** Done
> **Source basis:** PDF:I.1.1, PDF:I.0, PDF:I.1.4, PDF:I.15, PDF:II.0-II.1, PDF:II.2, PDF:II.5, PDF:III.0-III.2
> **Access date:** PDF source 2026-07-29; artifact verification 2026-08-02
> **Approver:** Semih — 2026-08-03
> **Decision type:** Business decision (PDF baseline + named approver)

## Decision

PDF:I.1.1 defines the single communication model verbatim:

> "Internal communication: direct application calls where appropriate + domain
> events/integration events"

The record therefore locks one model, not two conflicting ones:

1. **Direct application call** — only for interactions that must share a
   transaction/consistency boundary within the single deployment
   (PDF:I.1.1 "Single application/backend deployment", "Single PostgreSQL
   instance"; PDF:III.1.1). The caller invokes the owning module's public
   contract in-process.
2. **Domain/integration event** — for interactions where eventual consistency
   is accepted and for external callback/retry/reconciliation flows
   (PDF:I.15 idempotency; PDF:II.6.11 provider callbacks are not assumed
   successful). The publisher owns the event contract; consumers never
   mutate publisher state.
3. **External integrations** — always through Adapter/Anti-Corruption Layer
   (PDF:I.1.1; PDF:I.1.4).
4. **No distributed broker** — Kafka/RabbitMQ class middleware is forbidden
   (PDF:I.1.1). Cross-module event transport, where required, uses the
   outbox pattern (PDF:I.1.1 "Outbox pattern: gerekli integration/event
   akışlarında kullanılabilir"); technical ownership V0-ARC-003.
5. **No service-per-domain deployment** (PDF:I.1.1, PDF:I.45).

## Complete interaction list (approved 2026-08-03)

"Where appropriate" is closed in this record. Direct-call edges below are the
complete set; every other cross-module interaction is event-based. An edge
means "module may invoke the target's public contract in-process for a
same-transaction flow"; incoming rows from other modules are not repeated.

| # | Bounded context (PDF:II.2) | Direct-call dependency (same transaction) | Integration events (publisher → consumers) | Source |
| --- | --- | --- | --- | --- |
| 1 | Identity & Authorization | none (cross-cutting; consumed by all modules for actor/role checks) | none | II.2.1 |
| 2 | Catalog | none | ProductCatalogChanged → Menu, Order, OnlineOrdering | II.2.2 |
| 3 | Table Management | none | TableOccupancyChanged → Order, Bill, QR Ordering | II.2.3, II.5.15 |
| 4 | Order | Identity (actor validation), Catalog (item snapshot), Table Management (table association) | OrderStateChanged → Kitchen, Bill, QR Ordering, Online Ordering, Reporting, Reconciliation | II.5.1, II.7 |
| 5 | Bill | Order (order items into bill), Identity | BillStateChanged → Payment, Fiscal, Reporting, Reconciliation | II.3.3, II.5.2, III.7 |
| 6 | Payment | Bill (allocation target), Identity | PaymentStateChanged → Bill, Fiscal, Meal Card, Customer Account, Reconciliation | II.5.3, III.8 |
| 7 | Cash | Payment (cash tender records) | CashSessionChanged → Reporting, Reconciliation | II.2.7, II.5.9 |
| 8 | Menu | Catalog (static catalog mapping) | MenuChanged → Daily Menu | II.2.8, II.2.9 |
| 9 | Daily Menu | Menu (daily availability), Catalog (prices) | DailyMenuChanged → Order, Online Ordering | II.2.9 |
| 10 | Recipe | Catalog (ingredient reference) | RecipeVersionChanged → Production | II.2.10, II.5.5 |
| 11 | Production | Recipe (immutable RecipeVersion), Inventory (portion output) | ProductionBatchChanged → Inventory, Reporting | II.2.11, II.5.5 |
| 12 | Inventory | none (movements are immutable ledger entries) | InventoryMovementRecorded → Recipe, Production, Reporting | II.2.12, II.5.14 |
| 13 | Kitchen | Order (order items), Identity | KitchenTicketChanged → Print, Reporting | II.2.13, II.5.7 |
| 14 | Print | Kitchen (print jobs) | PrintJobStateChanged → Kitchen, Observability | II.2.13, II.5.8 |
| 15 | Meal Card | Payment (meal card tenders) | MealCardSettlementChanged → Payment, Customer Account, Reconciliation | II.2.14, II.5.10 |
| 16 | Customer Account | Bill, Payment (account credits) | AccountTransactionRecorded → Bill, Invoicing, Reporting | II.2.15, II.5.11 |
| 17 | Fiscal | Payment (fiscal closure), Bill | FiscalDocumentChanged → Payment, Reconciliation, Observability | II.2.16, II.5.4 |
| 18 | Invoicing | Customer Account (invoice drafts), Fiscal | InvoiceChanged → QNB adapter, Reconciliation | II.2.17, II.5.11 |
| 19 | QR Ordering | Identity (public token), Table Management | QrOrderSubmitted → Order | II.2.18, II.5.15 |
| 20 | Online Ordering | Catalog, Daily Menu (availability) | OnlineOrderMapped → Order, Reconciliation | II.2.19 |
| 21 | Reporting | none (reads projections only) | none | II.2.20 |
| 22 | Reconciliation | Payment, Fiscal, Invoice, Print (mismatch sources) | ReconciliationCaseChanged → Observability, Reporting | II.2.21, II.5.12 |
| 23 | Audit | none (append-only event trail consumer) | none | II.2.22, II.9 |
| 24 | Backup | none (infrastructure) | BackupJobStateChanged → Observability | II.2.23 |
| 25 | Licensing | none (cross-cutting validation; consumed by composition) | none | II.2.24 |
| 26 | Observability | none (cross-cutting consumer) | none | II.2.25 |

Notes:

- Reporting, Audit, Observability and Licensing are cross-cutting; they never
  appear as direct-call targets of domain flows.
- Table state coupling to Order/Bill is an application-layer invariant, not a
  database constraint (PDF:II.5.15).
- The full edge list is verified acyclic by V1-FND-001 (dependency graph
  validation); a future edge requires an approved plan change.

## Rejected alternatives

1. **Event-only cross-module communication** — the withdrawn second half of
   the former record; contradicts PDF:I.1.1 ("direct application calls where
   appropriate") and the same-transaction boundary rule of PDF:II.5.
2. **Service-per-domain deployment** — PDF:I.1.1 "Service-per-domain
   deployment: YOK".
3. **Mandatory distributed broker (Kafka/RabbitMQ)** — PDF:I.1.1 "zorunlu
   distributed broker: YOK".

## Examples

Positive:

- Order creation allocates table association and records order items in one
  transaction → direct call Order → Table Management + Catalog (same boundary).
- Bill closure recomputes allocation sums with payment changes in one
  transaction → direct call Payment → Bill.

Negative:

- Kitchen must never call Payment directly to reverse a tender; reversal flows
  through Order/Payment domain events (PDF:I.16 "Order state ile kitchen state
  aynı değildir").
- Reporting must never call domain modules to mutate state; it consumes
  projection-ready events only.

## Affected tasks

- Consumers blocked on this record (dependency rows): V0-ARC-002, V0-ARC-003,
  V0-ARC-004, V0-ARC-005, V0-ARC-007, V0-ARC-009, V0-CMP-003, V0-DAT-005,
  V0-DOC-001, V1-FND-002, V1-FND-003, V1-FND-004, V1-FND-005, V1-FND-006,
  V1-FND-007, V1-FND-008, V1-FND-009, V1-FND-011, V1-FND-012, V1-IAM-001,
  V1-SEC-001, V1-SEC-002, V1-SEC-003, V20-DOC-002.
- Handoff: V1-FND-001 (module composition contract, graph acyclicity check).

## Acceptance evidence

- Decision record with source, access dates, approver, selected result,
  rejected alternatives and affected task IDs: above.
- Dependency graph acyclicity and per-edge ownership are validated at
  V1-FND-001 (module composition contract).
