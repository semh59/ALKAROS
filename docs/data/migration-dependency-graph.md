# Migration Dependency Graph

> **Task:** V0-DAT-001
> **Status:** Blocked
> **Assignee:** codex-v0-dat-001
> **Work type:** decision
> **Source basis:** PDF:II.0-II.1, PDF:III.0-III.2, PDF:II.13-II.15, PDF:III.29-III.40, CORR:C1
> **Date:** 2026-07-30
> **2026-08-01 kayıtlı güncelleme (V1-FND-002 kapsamı, V0-DAT-001 sahipliğinde):** Altyapı tabloları
> `idempotency_keys`, `inbox_messages` ve `outbox_messages` eklendi. Bu tablolar hiçbir domain tablosuna FK
> taşımaz ve domain tablolarından kendilerine FK taşınmaz (örn. `payment_allocations.idempotency_key` V0-DOM-004
> kararı gereği FK değil, düz unique kolondur); bu yüzden Phase A'nın başında, pozisyonlar 001-003'te konumlanır.
> Pozisyon kaydı `database/MigrationComposition/order.json` içindedir; her pozisyon tek tablo içerir ve ileri/geri
> scriptleri vardır. Kalan domain tabloları pozisyon atamalarını kendi görevlerinde alır.
>
> **2026-08-02 kayıtlı güncelleme (V1-IAM-001 kapsamı, V0-DAT-001 sahipliğinde):** 004 pozisyonu `stores`, `users`,
> `roles` üçlüsünden ayrıldı: 004 `stores`, 005 `users` (V1-IAM-001), 006 `roles` (V1-IAM-002) olur; sonraki
> pozisyonlar 005-024 → 007-026 olarak +2 kayar (Phase B 031-035 değişmez). Kayma, migration scriptleri henüz
> oluşturulmadan yapıldığı için mevcut hiçbir scripti etkilemez.

## 1. Entity Dependency Graph

> Dependency depth labels (Layer 1-4) — execution uses Phase A/B (Section 2); Layer N is NOT a migration phase.

```text
Layer 1 (no FK dependencies):
  ┌─────────────────────────────┐
  │ idempotency_keys            │
  │ inbox_messages              │
  │ outbox_messages             │
  │ backup_logs                 │
  │ stores                      │
  │ users                       │
  │ roles                       │
  │ printers                    │
  │ printer_routes              │
  │ products                    │
  │ categories                  │
  │ units                       │
  │ suppliers                   │
  │ payment_providers           │
  │ meal_card_providers         │
  │ fiscal_devices              │
  └─────────────────────────────┘
            │
            ▼
Layer 2 (FK to Layer 1):
  ┌─────────────────────────────┐
  │ tables                      │
  │ reservations                │
  │ orders                      │
  │ order_items                 │
  │ recipes                     │
  │ recipe_items                │
  │ daily_menus                 │
  │ menu_items                  │
  │ purchase_orders             │
  │ purchase_order_items        │
  └─────────────────────────────┘
            │
            ▼
Layer 3 (FK to Layer 1-2):
  ┌─────────────────────────────┐
  │ bills                       │
  │ bill_order_items            │
  │ payments                    │
  │ payment_allocations         │
  │ fiscal_documents            │
  │ kitchen_tickets             │
  │ kitchen_ticket_items        │
  │ print_jobs                  │
  │ cash_sessions               │
  │ cash_transactions           │
  │ meal_card_settlements       │
  └─────────────────────────────┘
            │
            ▼
Layer 4 (FK to Layer 1-3):
  ┌─────────────────────────────┐
  │ invoices                    │
  │ invoice_lines               │
  │ customer_accounts           │
  │ account_transactions        │
  │ reconciliation_cases        │
  │ alerts                      │
  │ stock_ledger_entries        │
  │ stock_balances              │
  │ production_batches          │
  │ portion_reservations        │
  │ refund_ledger_entries       │
  └─────────────────────────────┘
```

## 2. Migration Strategy

### Two-Phase Approach

Due to circular dependencies between Fiscal-Invoicing and CustomerAccount-Invoicing, a single-phase migration is not
possible. The migration is split into two phases:

**Phase A (Migrations 001-030):** All tables except those in the fiscal-invoicing cycle.
**Phase B (Migrations 031-040):** Remaining tables with deferred FK constraints.

### Circular Dependency Resolution

```text
Cycle: fiscal_documents → invoices → customer_accounts → payments → fiscal_documents
Resolution: Phase A creates all tables WITHOUT the cycle FKs.
            Phase B adds the cycle FKs as ALTER TABLE ADD CONSTRAINT.
```

### Deferred FK Pattern

```sql
-- Phase A: Create table without cycle FK
CREATE TABLE invoices (
    id UUID PRIMARY KEY,
    -- ... other columns
    -- fiscal_document_id added in Phase B
);

-- Phase B: Add cycle FK
ALTER TABLE invoices ADD COLUMN fiscal_document_id UUID REFERENCES fiscal_documents(id);
```text

## 3. Table Creation Order (Phase A and Phase B)

> V1-FND-002 (2026-08-01): altyapı tabloları ayrı pozisyonlarda, adım listesinin başında konumlanır —
> `idempotency_keys` → 001, `inbox_messages` → 002, `outbox_messages` → 003 (her biri tek tabloluk ileri/geri
> script; kayıt `database/MigrationComposition/order.json`).

### Phase A (migrations 001-030)

1. stores, users, roles
2. printers, printer_routes
3. products, categories, units
4. suppliers, payment_providers, meal_card_providers, fiscal_devices
5. tables, reservations
6. orders, order_items
7. recipes, recipe_items
8. daily_menus, menu_items
9. purchase_orders, purchase_order_items
10. bills, bill_order_items
11. payments, payment_allocations
12. fiscal_documents (without invoice FK)
13. kitchen_tickets, kitchen_ticket_items
14. print_jobs
15. cash_sessions, cash_transactions
16. meal_card_settlements
17. customer_accounts, account_transactions
18. stock_ledger_entries, stock_balances
19. production_batches, portion_reservations
20. refund_ledger_entries
21. reconciliation_cases, alerts

### Phase B (migrations 031-040)

 1. invoices, invoice_lines (migration 031; cycle FKs applied in migrations 032-035, see Section 4)

## 4. Deferred Constraints (Phase B)

```sql
-- Fiscal-Invoicing cycle
ALTER TABLE invoices ADD COLUMN fiscal_document_id UUID REFERENCES fiscal_documents(id);
ALTER TABLE fiscal_documents ADD COLUMN invoice_id UUID REFERENCES invoices(id);

-- CustomerAccount-Invoicing cycle
ALTER TABLE invoices ADD COLUMN customer_account_id UUID REFERENCES customer_accounts(id);
ALTER TABLE account_transactions ADD COLUMN invoice_id UUID REFERENCES invoices(id);
```

## 5. Invariants

1. **No forward references**: Phase A tables MUST NOT reference Phase B tables.
2. **Deferred FK documentation**: Every deferred FK MUST be documented with its phase and ALTER statement.
3. **Cycle-free within phase**: Each phase MUST be internally acyclic.
4. **Idempotent**: Each migration MUST be idempotent (IF NOT EXISTS / IF EXISTS).
5. **Rollback**: Each migration MUST have a corresponding rollback script.

## 6. Positive Examples

### Example 1: Simple FK (no cycle)

- `orders.store_id` REFERENCES `stores(id)` — both in Phase A, stores created before orders. ✅

### Example 2: Deferred FK (cycle)

- `invoices.fiscal_document_id` REFERENCES `fiscal_documents(id)` — cycle detected, deferred to Phase B. ✅

## 7. Negative Examples

### Example 1: Forward reference

- Phase A migration attempts to create FK to a Phase B table.
- Result: Migration rejected — must be deferred to Phase B.

### Example 2: Undocumented deferred FK

- A deferred FK is added in Phase B without documentation.
- Result: Audit failure — all deferred FKs must be documented.

## 8. Consumer Task Interface

### Input

```json
{
  "entityName": "invoices",
  "phase": "B"
}
```text

### Output

```json
{
  "creationOrder": "031",
  "dependencies": ["fiscal_documents", "customer_accounts"],
  "deferredFks": ["fiscal_document_id", "customer_account_id"]
}
```

> `creationOrder` değerleri `database/MigrationComposition/order.json` migration id'leridir; `backup_logs` bu V0
> aralığında (001-040) değildir ve pozisyonunu backup modülü görevinde alır.

## 9. Affected Tasks

- GATE-V0-EXIT (migration graph must be cycle-free or explicitly two-phase)
