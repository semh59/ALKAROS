# V15-KVK-001 - Implement KVKK retention execution

- Task ID: V15-KVK-001
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:II.11-II.12
- PDF:III.33-III.34

## Goal

Onaylanan veri envanterini değerlendirin ve tüm mağazalarda uygun silme/anonimleştirme işlemlerini planlayın.

## Owned surface

- `src/Modules/Privacy/RetentionExecution/**`, `tests/Modules/Privacy/RetentionExecution/**`,
  `database/migrations/V15/V15-KVK-001/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Politika sürümü, vade seçimi, yasal bekletme, prova, idempotency ve denetim.

## Out of scope

- Alan düzeyinde anonimleştirme uygulaması.

## Dependencies

- V0-CMP-003
- V13-CST-002
- V15-SEC-003

## Deliverables

- `src/Modules/Privacy/RetentionExecution/**` altında Goal kapsamını uygulayan production code ve task-specific
  automated test assets.
- Başarı, ret/failure ve recovery testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Prova ve yürütme aynı uygun kayıtları seçer; yasal bekletme mutasyonu engeller; tekrarlanan çalışma stabildir.

## Handoff

- V15-KVK-002
- V20-CMP-001
