# V1-KDS-009 - Mutfak performans raporu ekranı

- Task ID: V1-KDS-009
- Status: Done
- Assignee: Claude Sonnet 5
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
- src/Clients/PosTerminal/src/routes/workspace.tsx (Sınırlı ek, paylaşılan
  — V1-KDS-001 sahipliğinde kalan dosya) — `KitchenRoute`'a `canViewReports`
  (`capabilitySet.has("reports.view")`) ve `onLoadPerformanceReport`
  prop'larını ekler; V1-KDS-002/V1-KDS-006 emsaliyle aynı desen.

## Out of scope

- `V1-KIT-014`'ün kendisi.
- Grafik kütüphanesi eklemek — basit bar/çubuk gösterimi mevcut
  tasarım sistemiyle (CSS) yapılır, yeni bir bağımlılık eklenmez.

## Dependencies

- V1-KIT-014

## Acceptance evidence

- `cd src/Clients/PosTerminal && npx tsc --noEmit` → **0 hata** (doğrulandı).
- `cd src/Clients/PosTerminal && npx vitest run` → **Test Files 23 passed
  (23), Tests 165 passed (165)** — tüm proje, izole değil (4 yeni test:
  "Rapor" düğmesinin `canViewReports=false`'ta VE `onLoadPerformanceReport`
  hiç verilmediğinde gizlendiği (iki ayrı koşul, ayrı ayrı test edildi);
  rapor açılınca ortalama (15.0 dk) VE medyanın (10.0 dk — farklı değer,
  testin anlamlı olduğunu kanıtlıyor) VE hedef aşım yüzdesinin (%33.3)
  ekranda gerçekten göründüğü; yükleme başarısız olunca sınırlı bir hata
  mesajı gösterildiği).
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0
  uyarı (doğrulandı; ilk yazımda 1 Türkçe-karakter-in-İngilizce-yorum
  ihlali bulundu ve düzeltildi).
- `python tools/consistency-audit/consistency_audit.py` → `clean`
  (doğrulandı).
- Uygulanan tasarım: Expo/Tüm Gün'ün yanına üçüncü bir görünüm modu
  ("Rapor"), yalnız `canViewReports && onLoadPerformanceReport` ikisi de
  doğruyken düğme render edilir (gizli, V1-KDS-002'nin "kilitli ama
  görünür" desenini bilinçli olarak takip etmiyor — bu ayrı bir operasyon
  ekranı, küçük bir aksiyon değil). Rapor yalnız "Rapor" moduna
  geçildiğinde çekilir (workspace'in kendi 8sn'lik polling'ine dahil
  değil), varsayılan aralık bugün (UTC gece yarısından şu ana kadar).
- Semih'in elle deneyebileceği senaryo: birkaç bilet tamamla, performans
  panelini aç, istasyon bazlı ortalama/medyan sürelerin ve hedef aşım
  yüzdesinin gerçek verilerle eşleştiğini doğrula.

## Handoff

- None
