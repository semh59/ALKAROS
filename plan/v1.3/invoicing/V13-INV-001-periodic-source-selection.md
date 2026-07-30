# V13-INV-001 - Implement periodic invoice source selection

- Task ID: V13-INV-001
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:I.30-I.33
- PDF:II.2.17
- PDF:II.5.11
- PDF:III.20

## Goal

Bakiyeyi değiştirmeden kapalı bir fatura dönemi için uygun faturalanmamış CustomerAccount işlemlerini seçin.

## Owned surface

- `src/Modules/Invoicing/SourceSelection/**`, `tests/Modules/Invoicing/SourceSelection/**`,
  `database/migrations/V13/V13-INV-001/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Dönem sınırı, uygunluk, kilitleme, kaynağın benzersizliği ve yeniden çalıştırma davranışı.

## Out of scope

- Invoice oluşturma/provider gönderimi ve gelen faturalar.

## Dependencies

- V13-ACC-002
- V13-ACC-003
- V0-CMP-002

## Deliverables

- `src/Modules/Invoicing/SourceSelection/**` altında Goal kapsamını uygulayan production code ve task-specific automated
  test assets.
- Başarı, ret, retry/idempotency ve veri bütünlüğü testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Bir işlem en fazla bir adet iptal edilmemiş invoice kaynak kümesine aittir; yeniden çalıştırma aynı kilitli seti
  döndürür veya çalışmaz.

## Handoff

- V13-INV-002
- V13-INV-003
