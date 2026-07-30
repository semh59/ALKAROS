# V11-INV-001 - Implement immutable StockMovement ledger

- Task ID: V11-INV-001
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:II.2.12
- PDF:II.3.9
- PDF:II.5.6
- PDF:II.5.14
- PDF:III.14

## Goal

Tiplendirilmiş stok hareketlerini pozitif büyüklük, yön kuralları ve kaynak referanslarıyla uygulayın.

## Owned surface

- `src/Modules/Inventory/MovementLedger/**`, `tests/Modules/Inventory/MovementLedger/**`,
  `database/migrations/V11/V11-INV-001/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Kanonik hareket türleri, birim/para birimi içermeyen miktar kuralları, kaynak ayırıcı uygulaması ve yalnızca eklemeli
  depolama.

## Out of scope

- Önbelleğe alınmış stok bakiyeleri ve rezervasyon geçişleri.

## Dependencies

- V11-INV-004
- V11-UNT-001
- V0-DAT-002
- V0-DAT-003

## Deliverables

- `src/Modules/Inventory/MovementLedger/**` altında Goal kapsamını uygulayan production code ve task-specific automated
  test assets.
- Aynı davranışın otomatik başarı, ret ve concurrency/idempotency testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Hareket satırları güncellenemez/silinemez; geçersiz kaynak türü veya işareti reddedildi; Her hareketin deterministik
  stok etkisi vardır.

## Handoff

- V11-INV-002
- V11-INV-003
- V11-RSV-001
