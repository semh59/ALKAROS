# V1-KIT-002 - Implement deterministic printer routing

- Task ID: V1-KIT-002
- Status: Done
- Assignee: Antigravity-v1-kit-002
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:I.16-I.20
- PDF:II.2.13
- PDF:II.3.13-II.3.14
- PDF:II.5.7-II.5.8
- PDF:II.8
- PDF:III.16

## Goal

Her kitchen item'ı tam bir configured station/printer route'a veya açık configuration error sonucuna çözmek.

## Owned surface

- `src/Modules/Kitchen/Routing/**`, `tests/Modules/Kitchen/Routing/**`, `database/migrations/V1/V1-KIT-002/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Ürün/kategori/günlük öğe önceliği, belirsizlik tespiti ve etkin olmayan yazıcı yönetimi.

## Out of scope

- Yazdırma kuyruğu retry ve device protokolü.

## Dependencies

- V1-KIT-001
- V1-CAT-001
- V0-DOM-011

## Deliverables

- `src/Modules/Kitchen/Routing/**` altında Goal kapsamını uygulayan production code ve task-specific automated test
  assets.
- Owned surface içinde otomatik başarı, ret ve edge-case testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Belirsiz rotalar yapılandırma sırasında reddedilir; Her yönlendirilebilir öğe testlerde deterministik olarak
  çözümlenir.

## Handoff

- V1-KIT-003
