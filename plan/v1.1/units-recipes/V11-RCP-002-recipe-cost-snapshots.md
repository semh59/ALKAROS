# V11-RCP-002 - Implement reproducible recipe cost snapshots

- Task ID: V11-RCP-002
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:I.21-I.25
- PDF:II.2.10
- PDF:II.3.7
- PDF:III.12

## Goal

Geçmiş tarif maliyetini yeniden oluşturmak için gereken içerik düzeyindeki maliyet esasını sürdürün.

## Owned surface

- `src/Modules/Recipes/CostSnapshots/**`, `tests/Modules/Recipes/CostSnapshots/**`,
  `database/migrations/V11/V11-RCP-002/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- İçerik fiyat kaynağı, atık faktöründen sonraki miktar, dönüşüm, para birimi ve anlık görüntü satırları.

## Out of scope

- Tedarikçi hesabı gönderimi ve production toplu yürütme.

## Dependencies

- V11-RCP-001
- V11-UNT-001
- V11-PUR-001
- V0-DOM-010

## Deliverables

- `src/Modules/Recipes/CostSnapshots/**` altında Goal kapsamını uygulayan production code ve task-specific automated
  test assets.
- Aynı davranışın otomatik başarı, ret ve concurrency/idempotency testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Anlık görüntü toplamı, değiştirilemez satırlardan yeniden hesaplanır ve daha sonraki fiyat/birim güncellemelerinden
  sonra değişmeden kalır.

## Handoff

- V11-PRD-001
- V13-PUR-001
