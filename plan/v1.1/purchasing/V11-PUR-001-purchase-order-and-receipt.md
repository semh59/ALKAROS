# V11-PUR-001 - Implement purchase-order items and goods receipt

- Task ID: V11-PUR-001
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:III.15

## Goal

Supplier PurchaseOrder ve line item'ları, StockLedger'a kayıtlı receipt movement'larıyla uygulamak.

## Owned surface

- `src/Modules/Purchasing/OrdersAndReceipts/**`, `tests/Modules/Purchasing/OrdersAndReceipts/**`,
  `database/migrations/V11/V11-PUR-001/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- order satırları, kısmi giriş, miktar/birim/fiyat anlık görüntüleri ve PurchaseReceipt hareketlerini satın alın.

## Out of scope

- Tedarikçiye ödenecek hesap ve gelen e-invoice eşleşmesi.

## Dependencies

- V11-INV-004
- V11-PUR-002
- V11-INV-001
- V11-UNT-001
- V1-CAT-001
- V0-DOM-009

## Deliverables

- `src/Modules/Purchasing/OrdersAndReceipts/**` altında Goal kapsamını uygulayan production code ve task-specific
  automated test assets.
- Aynı davranışın otomatik başarı, ret ve concurrency/idempotency testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Kısmi ve kesin girişler, tam stok hareketlerini bir kez kaydeder; fazla alım, açık politika sonucunu gerektirir.

## Handoff

- V13-PUR-001
