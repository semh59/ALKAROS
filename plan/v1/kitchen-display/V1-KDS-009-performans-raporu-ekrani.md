# V1-KDS-009 - Mutfak performans raporu ekranı

- Task ID: V1-KDS-009
- Status: Planned
- Assignee: Unassigned
- Work type: implementation
- Surface state: Existing

## Goal

`V1-KIT-014`'ün ürettiği performans raporu ucunu Mutfak ekranına bağlar.
İstasyon bazlı ortalama/medyan süre, hedef aşım yüzdesi, saatlik hacim
grafiği. Araştırmanın kendi dersi: ortalama VE medyan YAN YANA gösterilir
(yalnız ortalama göstermek yanıltıcı olabilir — bkz. araştırma dosyası
§3). Backend `reports.view` (`ApplicationPermissions.ReportsView`) ile
korunuyor (V1-KIT-014); frontend'de görünürlük capability set'ten
`capabilitySet.has("reports.view")` ile türetilen yeni bir `canViewReports`
prop'uyla kapılanır — bugün yalnız supervisor/manager taşıyor.

## Owned surface

- src/Clients/PosTerminal/src/features/kitchen-operations/** (Sınırlı ek
  — V1-KDS-001 sahipliğinde kalan dosyalar) — yeni bir rapor
  bileşeni/panel, `kitchenApi.ts`'e yeni bir çağrı.

## Out of scope

- `V1-KIT-014`'ün kendisi.
- Grafik kütüphanesi eklemek — basit bar/çubuk gösterimi mevcut
  tasarım sistemiyle (CSS) yapılır, yeni bir bağımlılık eklenmez.

## Dependencies

- V1-KIT-014

## Acceptance evidence

- `cd src/Clients/PosTerminal && npx tsc --noEmit` → 0 hata.
- `cd src/Clients/PosTerminal && npx vitest run` → tüm proje yeşil (yeni
  testler: rapor panelinin ortalama VE medyanı ayrı ayrı gösterdiği;
  `canViewReports=false` bir oturumda panelin görünmediği).
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0
  uyarı.
- `python tools/consistency-audit/consistency_audit.py` → `clean`.
- Semih'in elle deneyebileceği senaryo: birkaç bilet tamamla, performans
  panelini aç, istasyon bazlı ortalama/medyan sürelerin ve hedef aşım
  yüzdesinin gerçek verilerle eşleştiğini doğrula.

## Handoff

- None
