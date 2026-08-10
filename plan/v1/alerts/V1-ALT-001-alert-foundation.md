# V1-ALT-001 - Implement Alert foundation

- Task ID: V1-ALT-001
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:I.16-I.20
- PDF:II.2.25
- PDF:II.5.13
- PDF:III.28

## Goal

Rule-based Alert lifecycle, source reference, deduplication ve notification audit davranışını uygulamak.

## Owned surface

- `src/Modules/Observability/AlertFoundation/**`, `tests/Modules/Observability/AlertFoundation/**`,
  `database/migrations/V1/V1-ALT-001/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Bilgi/Uyarı/Kritik önem derecesi, yaşam döngüsü, yinelenenleri kaldırma anahtarı ve kaynak kataloğu.

## Out of scope

- Bildirim teslimi, yükseltme planı ve durum değerlendirmesi.

## Dependencies

- V1-FND-001
- V0-DOM-001
- V0-DAT-002

## Deliverables

- `src/Modules/Observability/AlertFoundation/**` altında Goal kapsamını uygulayan production code ve task-specific
  automated test assets.
- Public API/event contract varsa contract testleri dahil otomatik testler.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Tekrarlanan aynı kaynak/hata, bir aktif alert sağlar; onay/çözüm aktörü ve zaman damgaları korunur.

## Handoff

- V15-OBS-002
- V15-NOT-001
