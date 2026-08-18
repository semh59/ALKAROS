# V1-REC-001 - Implement ReconciliationCase foundation

- Task ID: V1-REC-001
- Status: Done
- Assignee: Antigravity-v1-rec-001
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:I.16-I.20
- PDF:II.2.21
- PDF:II.3.15
- PDF:II.5.12
- PDF:II.6.11
- PDF:III.23

## Goal

Canonical ReconciliationCase lifecycle, paired source reference, open-case deduplication ve append-only event/action
yapısını uygulamak.

## Owned surface

- `src/Modules/Reconciliation/CaseFoundation/**`, `tests/Modules/Reconciliation/CaseFoundation/**`,
  `database/migrations/V1/V1-REC-001/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Vaka kimliği, kaynak çifti, status geçişleri, önem derecesi, nedeni ve benzersizliği.

## Out of scope

- Payment/QNB/çevrimiçi'ne özel dedektörler ve kontrol paneli.

## Dependencies

- V1-FND-001
- V0-DOM-001
- V0-DAT-002

## Deliverables

- `src/Modules/Reconciliation/CaseFoundation/**` altında Goal kapsamını uygulayan production code ve task-specific
  automated test assets.
- Public API/event contract varsa contract testleri dahil otomatik testler.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Aynı uyumsuzluk anahtarı bir açık vaka oluşturur; yasak geçişler başarısız olur; geçmiş güncellenemez/silinemez.

## Handoff

- V12-REC-001
- V13-QNB-004
- V14-REC-001
- V15-REC-001
