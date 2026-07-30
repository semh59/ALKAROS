# V11-INV-005 - Implement manual inventory adjustment

- Task ID: V11-INV-005
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

Bakiyeleri doğrudan düzenlemeden, zorunlu gerekçeyle izin verilen Ayarlama hareketlerini yayınlayın ve denetleyin.

## Owned surface

- `src/Modules/Inventory/ManualAdjustments/**`, `tests/Modules/Inventory/ManualAdjustments/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Arttırma/azaltma yönü, olumsuz olmayan sonuç, izin, sebep ve idempotency.

## Out of scope

- Atık, iadeler ve satın alma makbuzu.

## Dependencies

- V11-INV-001
- V11-INV-002
- V1-IAM-002
- V1-OPS-001

## Deliverables

- `src/Modules/Inventory/ManualAdjustments/**` altında Goal kapsamını uygulayan production code ve task-specific
  automated test assets.
- Public contract ve otomatik başarı/ret/concurrency testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Yetkisiz veya olumsuz sonuçlu ayarlama başarısız olur; başarılı ayarlama, tek bir hareket ve eşleşen projeksiyon
  üretir.

## Handoff

- V11-UI-003
- V11-RPT-001
