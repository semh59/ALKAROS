# V1-ORD-003 - Implement item void and complimentary commands

- Task ID: V1-ORD-003
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:II.2.4
- PDF:II.3.2
- PDF:II.5.1
- PDF:III.6

## Goal

Onaylı void/complimentary politikasını permission, reason, audit ve kitchen-state kontrolleriyle uygulamak.

## Owned surface

- `src/Modules/Orders/ItemExceptions/**`, `tests/Modules/Orders/ItemExceptions/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Mutfak öncesi/sonrası uygunluk, sıfır fiyat sonucu, denetim ve alt event.

## Out of scope

- İade, atık stok etkisi ve indirim hesaplaması.

## Dependencies

- V1-ORD-001
- V1-IAM-002
- V1-KIT-001
- V1-OPS-001
- V0-DOM-006

## Deliverables

- `src/Modules/Orders/ItemExceptions/**` altında Goal kapsamını uygulayan production code ve task-specific automated
  test assets.
- Public API/event contract varsa contract testleri dahil otomatik testler.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Yetkisiz/geç iptal başarısız olur; Ücretsiz olarak teslim edilen miktar ve politikanın gerektirdiği vergi/mali
  girdiler korunur.

## Handoff

- V11-RSV-003
- V1-BIL-003
