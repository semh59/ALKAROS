# V11-INV-006 - Implement general waste recording

- Task ID: V11-INV-006
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

production'den, porsiyon rezervasyonundan veya manuel onaylı kaynaktan izlenebilir Atık hareketlerini kaydedin.

## Owned surface

- `src/Modules/Inventory/WasteRecording/**`, `tests/Modules/Inventory/WasteRecording/**`,
  `database/migrations/V11/V11-INV-006/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Kaynak, miktar/birim, sebep, izin, denetim ve bakiye etkisi yazılır.

## Out of scope

- Payment iade, manuel ayarlama ve iptal sınıflandırması.

## Dependencies

- V11-INV-001
- V11-INV-002
- V1-IAM-002
- V1-OPS-001

## Deliverables

- `src/Modules/Inventory/WasteRecording/**` altında Goal kapsamını uygulayan production code ve task-specific automated
  test assets.
- Public contract ve otomatik başarı/ret/concurrency testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Her atık kaydı bir kaynağı ve hareketi birbirine bağlar; yinelenen gönderimin tek bir etkisi vardır; hazırlanan ürün
  asla mevcut durumuna geri dönmez.

## Handoff

- V11-RPT-001
