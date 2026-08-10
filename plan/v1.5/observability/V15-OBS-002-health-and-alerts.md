# V15-OBS-002 - Implement health checks and alert lifecycle

- Task ID: V15-OBS-002
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:I.38-I.44
- PDF:II.2.25
- PDF:II.5.13
- PDF:III.28

## Goal

Veritabanı, disk, yazıcı, yedekleme ve entegrasyon durumunu tekilleştirilmiş uyarılarla değerlendirin.

## Owned surface

- `src/Modules/Observability/HealthAlerts/**`, `tests/Modules/Observability/HealthAlerts/**`,
  `database/migrations/V15/V15-OBS-002/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Kanonik sağlık/status değerleri, alert kuralları, onaylama/yükseltme/bastırma/çözümleme ve veri tekilleştirme.

## Out of scope

- Dış bildirim kanalları ve mutabakat çözümü.

## Dependencies

- V1-OPS-002
- V15-REC-001
- V0-DAT-002
- V0-DOM-001
- V1-ALT-001

## Deliverables

- `src/Modules/Observability/HealthAlerts/**` altında Goal kapsamını uygulayan production code ve task-specific
  automated test assets.
- Başarı, ret/failure ve recovery testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Tekrarlanan aynı hata, bir aktif alert sağlar; iyileşme sorunu çözer; eski/başarısız kontrol sağlıklı raporlanamaz.
- `V15-REC-001` kanıtlı `NotApplicable` ise reconciliation kaynaklı alert tetikleyicileri beklenmez; health kontrol ve
  iyileşme davranışı kendi kaynaklarıyla yine doğrulanır.

## Handoff

- V15-RUN-001
- V20-GAT-002
