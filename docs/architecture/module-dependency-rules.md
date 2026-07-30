# Module Dependency Rules

> **Task:** V0-ARC-001
> **Status:** InProgress
> **Assignee:** codex-v0-arc-001
> **Work type:** decision
> **Source basis:** PDF:I.0-I.5, PDF:II.0-II.1, PDF:III.0-III.2
> **Date:** 2026-07-30

## 1. Module List

| Module | Responsibility |
|--------|---------------|
| Shared | Primitives, base classes, utilities |
| Domain | Entities, value objects, domain events |
| Application | Use cases, CQRS handlers, DTOs |
| Infrastructure | EF Core, repositories, external services |
| Api | HTTP endpoints, middleware, DI composition |
| Orders | Order aggregate, order items |
| Billing | Bill aggregate, bill-order items |
| Payments | Payment aggregate, allocations |
| Kitchen | Kitchen tickets, printer routing |
| Catalog | Products, categories, pricing |
| Tables | Table lifecycle, reservations |
| Cash | Cash sessions, transactions |
| Fiscal | Fiscal documents, device integration |
| Inventory | Stock ledger, balances, production |
| Accounts | Customer accounts, invoicing |
| Reporting | Read models, reports |
| Reconciliation | Reconciliation cases |
| Notifications | Notification delivery |
| Settings | Typed settings, secrets |
| Identity | Auth, authorization, device sessions |

## 2. Dependency Rules

### Allowed Dependencies (→)
```
Api → Application → Domain
Infrastructure → Application → Domain
Application → Domain
Orders → Shared, Domain
Billing → Orders, Shared, Domain
Payments → Billing, Shared, Domain
Kitchen → Orders, Shared, Domain
Catalog → Shared, Domain
Tables → Shared, Domain
Cash → Payments, Shared, Domain
Fiscal → Payments, Billing, Shared, Domain
Inventory → Catalog, Shared, Domain
Accounts → Billing, Payments, Shared, Domain
Reporting → All (read-only projections)
Reconciliation → Payments, Cash, Accounts
Notifications → Shared
Settings → Shared
Identity → Shared
```

### Forbidden
1. Domain → any (Domain depends on nothing)
2. Infrastructure → Api (no reverse dependency)
3. Cross-aggregate direct references (use IDs, not entity references)
4. Reporting → write operations (read-only)

### Shared Kernel
- Shared module contains: Entity base, ValueObject base, DomainEvent base, Result type, Guard clauses.
- No business logic in Shared.

### Integration Events
- Each module owns its integration events.
- Cross-module communication via integration events only (no direct calls).
- Event naming: `<Module>.<Entity>.<Action>` (e.g., `Billing.Bill.Settled`)

## 3. Invariants
1. Dependency graph is acyclic.
2. Every cross-module call has a defined owner.
3. No module depends on Infrastructure or Api.
4. Domain has zero external dependencies.

## 4. Affected Tasks
- V1-FND-001 (module skeleton)