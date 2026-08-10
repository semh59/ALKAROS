# V13-INV-003 - Implement invoice line to source traceability

- Task ID: V13-INV-003
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

Her invoice satırını, onu üreten tam hesap işlem kümesiyle eşleyin.

## Owned surface

- `src/Modules/Invoicing/SourceTraceability/**`, `tests/Modules/Invoicing/SourceTraceability/**`,
  `database/migrations/V13/V13-INV-003/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Yazılan kaynak bağlantısı, satır/kaynak birleşimi, benzersizlik ve tutulan denetim yolu.

## Out of scope

- Invoice oluşturma ve provider status taşıma.

## Dependencies

- V13-INV-001
- V13-INV-002

## Deliverables

- `src/Modules/Invoicing/SourceTraceability/**` altında Goal kapsamını uygulayan production code ve task-specific
  automated test assets.
- Başarı, ret, retry/idempotency ve veri bütünlüğü testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Her satır bir veya daha fazla kaynağa uzanır; seçilen her kaynak temsil edilir; yetim ve yinelenen bağlantılar
  kısıtlamalarda başarısız olur.

## Handoff

- V13-QNB-002
