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
| 3 | Table Management | none | TableMerged / TableTransferred / TableUnmerged → Order, Bill (reparent still-active orders/bills after a merge / transfer / unmerge; v1-wave25 — via the transactional outbox, not a direct call); TableOccupancyChanged → Order, Bill, QR Ordering | II.2.3, II.5.15 |
| 4 | Order | Identity (actor validation), Catalog (item snapshot), Table Management (table association) | OrderStateChanged → Kitchen, Bill, QR Ordering, Online Ordering, Reporting, Reconciliation | II.5.1, II.7 |
| 5 | Bill | Order (order items into bill), Identity, Payment (the separate Billing.PaymentClosure module reads approved Payments and allocations to project payment-satisfied and to close a fully paid Bill; V13-ALC-002, V1-RMD-276) | BillStateChanged → Payment, Fiscal, Reporting, Reconciliation | II.3.3, II.5.2, III.7 |
| 6 | Payment | Bill (allocation target), Identity | PaymentStateChanged → Bill, Fiscal, Meal Card, Customer Account, Reconciliation | II.5.3, III.8 |
| 7 | Cash | Payment (cash tender records), Bill (tender allocation target) | CashSessionChanged → Reporting, Reconciliation | II.2.7, II.5.9 |
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
| 24 | Backup | Observability (structured alert logging on upload failure, V15-BKP-001), Security (off-site envelope encryption keyed through V15-SEC-001's secret rotation, V15-BKP-001) | BackupJobStateChanged → Observability | II.2.23 |
| 25 | Licensing | none (cross-cutting validation; consumed by composition) | none | II.2.24 |
| 26 | Observability | none (cross-cutting consumer) | none | II.2.25 |
| 27 | Purchasing | Inventory (goods receipt stock movement) | none yet | 2026-09-06 addition |
| 28 | Support | Observability (system status summary), Audit (selected correlation logs, bundle provenance) | none | V15-SUP-001, 2026-09-22 addition |
| 29 | Security | Identity (session/lockout hardening composes with AuthenticationService/IUserStore/IDeviceSessionService), Audit (disposal/purge/re-encryption trail) | none | V15-SEC-001/002/003, 2026-09-22 addition (found undeclared by an independent audit — Operations already had a real, undeclared ProjectReference on Security for V15-BKP-001's envelope encryption; Security itself was not a registered IModule at all, so it never appeared in dependency-boundary checks) |
| 30 | Customer Data (PII/profile boundary + anonymization request state machine) | Audit (V14-CST-002's own anonymization-request audit trail) | none | V14-CST-001, 2026-09-28 addition (a customer's name/phone/email/address are protected through the SensitiveData boundary the same way Invoicing's QNB credentials already are). V14-CST-002, 2026-09-28: added the Audit edge. Row 16 (Customer Account) and row 18 (Invoicing) are expected to reference customer identity through this module once V14-ACC/V14-INV start — this row is the foundation those Faz 4 tasks build on, not a replacement for either. |
| 31 | Customer Accounts - Bill Charges (`CustomerAccounts.BillCharges`, the account-charge tender handler) | Customer Accounts (its own ledger), Customer Data (eligibility - the customer must exist and not be anonymized), Payments, Payments.Allocations.Persistence, Billing | none | V14-ACC-003, 2026-09-28 addition - the first task to actually exercise row 16's own pre-approved Bill/Payment edges (V14-ACC-001/002 deliberately did not); the Customer Data edge is new, needed only for the eligibility check. |
| 32 | Customer Accounts - Cash Receipts (`CustomerAccounts.CashReceipts`, the bill-independent cash account receipt) | Customer Accounts (its own ledger and account payment), Customer Data (the customer must exist and not be anonymized), Cash.TransactionLedger (the drawer's CashIn movement) | none | V14-ACC-005, 2026-09-29 addition - the first Customer Account path that moves cash; no Bill, Payment or allocation edge is used. |
| 33 | Invoicing - Source Selection (`Invoicing.SourceSelection`, periodic invoice source sets) | none (reads `customer_account.account_transactions` by plain SQL, the read-model pattern of Reconciliation.Payments) | none | V14-INV-001, 2026-09-29 addition - writes only the invoicing schema; the ledger is never written, so selecting sources never changes a balance. |
| 34 | Invoicing - Generation (`Invoicing.Generation`, invoice drafts with KDV groups and a buyer snapshot) | CustomerData (the buyer's name and tax identity through `ICustomerProfileStore`, Manager role) | none (reads `invoicing` source sets, `customer_account.account_transactions`, `payments.payments` and `billing` by plain SQL) | V14-INV-002, 2026-09-29 addition - writes only the invoicing schema; the ledger is never written, so generating an invoice never adds a second debit. |

Notes:

- Row 19 (QR Ordering → Table Management) was approved 2026-08-03 but
  unexercised in code until V12-QRO-002 (2026-09-09): a QR submission now
  locks the table row (`ITableRepository.GetByIdForUpdateAsync`) and
  transitions `Available → Reserved` in the same transaction as its own
  idempotency-ledger insert and outbox enqueue — refusing outright when the
  table is anything else, which is the actual anti-remote-abuse mechanism
  (an already-Occupied/Reserved/Cleaning/OutOfService table cannot be
  claimed by a QR submission). Row 4 (Order → Table Management) backfills
  the `current_order_id` cache pointer once `QrOrderSubmittedConsumer`
  materializes the real Order, via a new same-transaction
  `ITableRepository.LinkCurrentOrderAsync` overload — the table's
  `current_status` does not change again there, it was already set
  `Reserved` at submission time.
- Row 27 (Purchasing) was added 2026-09-06 when Purchasing's goods-receipt
  posting was found writing `inventory.stock_movements` directly with no
  declared dependency (a real V0-ARC-001 violation, not merely undeclared) —
  fixed by adding the project reference and `IModule.DependsOn` edge together
  with this row, per the edge-addition rule below.
- Row 11 (Production → Inventory) was approved 2026-08-03 but unbuilt until
  2026-09-06: `ProductionStockEffectService` posted directly to
  `inventory.stock_balances`/`inventory.stock_movements` via raw SQL instead
  of the declared edge. Fixed the same way as row 27 — Production now calls
  `IStockBalanceRepository`/`IStockMovementRepository` through the caller's
  own connection and transaction (new overloads on both interfaces), so the
  batch's atomic stock-sufficiency check and its effects still commit or roll
  back as one unit; the sufficiency check itself stays a same-transaction
  read of `inventory.stock_balances`, which V0-ARC-001 already allows.
- Reporting, Audit, Observability and Licensing are cross-cutting; they never
  appear as direct-call targets of domain flows.
- Table state coupling to Order/Bill is an application-layer invariant, not a
  database constraint (PDF:II.5.15). After a merge / transfer / unmerge, Table
  Management writes one table event to the outbox in its own transaction;
  Order and Bill move their own rows to the new table when the outbox delivers
  it (eventually consistent, at-least-once, idempotent). Table Management holds
  no compile-time or direct-call dependency on Order or Bill.
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

## Host/Experience orchestration edges

The edges above are module-to-module. A second, smaller class of edge exists
under `src/Host/Experience/**`: a Host orchestrator that calls straight into
several modules' public contracts for one same-transaction flow, rather than
one module calling another. The writes still go through each target module's
own repository contract (never raw cross-schema SQL), so this was never a
V0-ARC-001 violation in substance — but until V1-RMD-159 (2026-09-11, found
by the 2026-09-10 Garson audit) nothing checked or recorded these edges, so
a new one could be added or widened completely silently.

| Orchestrator | Namespace | Calls | Why |
| --- | --- | --- | --- |
| `OrderStockConsumptionService` | `ALKAROS.Host.Experience.Orders.OrderStockConsumption` | Inventory, Orders, Recipes | V1-RMD-143/144: decrements stock in the same transaction as an order's Accept/submit write, through `IStockBalanceRepository`/`IStockMovementRepository`/`IProductStockMappingRepository`/etc. V11-RCP-004 (2026-09-16) added Recipes: in the same transaction, it also records what the item's recipe says it should have consumed into an append-only `recipe.theoretical_consumption_records` shadow ledger (never touches `stock_balances`) — feeds the future actual-vs-theoretical variance report (V11-RPT-003). |
| `SentItemVoidStore` | `ALKAROS.Host.Experience.Orders.SentItemVoid` | Billing, Inventory, Kitchen, Orders | V1-RMD-154: voiding a line already sent to the kitchen reverses its stock consumption, cancels its kitchen ticket item, and updates the order — one transaction, four modules' contracts. |
| *(root, everything else under this tree)* | `ALKAROS.Host.Experience.Orders` | Audit, Billing, Identity, Inventory, Kitchen, Orders, Recipes, Settings | V1-RMD-215: found by an independent audit (2026-09-16) — the two entries above only ever matched their own narrow sub-namespace, so `OrderManagementEndpoints.cs` and every sibling file directly under this root (table-draft, waiter suggestion, shift summary, handoff notes, course firing, ...) was never checked by anything. Deliberately the union of every module actually referenced anywhere in this tree, so it only adds coverage rather than loosening the two rows above. Recipes added by V11-RCP-004 for the same reason as the narrower row above. Audit added by V1-RMD-244: the void/comp/void-sent endpoints' first real `IAuditEventStore.AppendAsync` calls. |
| *(root, everything under this tree)* | `ALKAROS.Host.Experience.KitchenOperations` | Audit, Identity, Kitchen, Operations, Orders, Settings | V1-RMD-215: same gap, for Kitchen's own Host orchestrator (`KitchenOperationsStore`/`KitchenOperationsEndpoints`) — never had any entry at all before this. |
| `RecipeCatalogMappingEndpoints` | `ALKAROS.Host.Experience.Recipes` | Identity, Recipes | V11-RCP-003: a manager-only surface recording which recipe a catalog product corresponds to — the first step of the V1.1 actual-vs-theoretical variance report chain. Self-contained (its own endpoint filter/auth), not a widening of Orders' edge; Orders still cannot reach Recipe directly per row 4 above. |

`tests/Architecture/ModuleBoundaries`'s `HostOrchestrationEdgesStayWithinTheApprovedList`
enforces this table now (see Enforcement below); add a new orchestrator to
both the test's `ApprovedHostOrchestrationEdges` and this table in the same
diff that introduces it. A namespace that is itself a parent of an
already-listed narrower one (the two root rows above) must approve at
least the union of its children's own lists, so it only adds coverage.

## Enforcement

Three automated gates keep code and this record in sync:

- `tests/Architecture/ModuleBoundaries` asserts every module's actual compile
  dependencies are declared in `IModule.DependsOn`, that `DependsOn` stays within
  the direct-call edges above, and that no module/integration project is an
  empty shell.
- `tests/Architecture/ModuleBoundaries`'s `HostOrchestrationEdgesStayWithinTheApprovedList`
  checks the Host/Experience orchestration edges above the same way, scoped
  to each orchestrator's own namespace (the module-level checks never see
  Host code at all, since Host isn't itself a registered module).
- `tools/consistency-audit` (rule 5) fails when a module issues an `UPDATE` /
  `INSERT` / `DELETE` against another module's PostgreSQL schema **as raw SQL
  naming that schema**. A module may **read** another module's relations for a
  same-transaction query or a reconciliation projection; it changes another
  module's rows only through that module's repository contract, or by
  publishing an event the owning module consumes. The append-only `audit`
  schema (AUD-01) is written by every module by design and is exempt. This
  rule is a text scan over raw SQL strings — it cannot see a cross-module
  write made *through* a repository call (exactly the shape the Host
  orchestration edges above use), so it depends on
  `HostOrchestrationEdgesStayWithinTheApprovedList` to catch a new edge of
  that kind; see `docs/CONSISTENCY_AUDIT.md` for the blind spot written out
  in full.

The cross-module event path (row 3 today): a producer writes an
`OutboxEnvelope` through `OutboxStore.EnqueueAsync` on its own connection and
transaction (`ALKAROS.Messaging`), so the event row commits with the domain
write. `OutboxDispatcherHostedService` drains `outbox_messages` and fans each
message out through `OutboxFanoutSink` to the module
`IIntegrationEventConsumer` registrations. A consumer changes only its own
module's schema and must be idempotent (delivery is at-least-once).

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
