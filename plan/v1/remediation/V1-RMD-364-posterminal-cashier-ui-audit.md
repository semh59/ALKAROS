# V1-RMD-364 - PosTerminal Cashier.tsx modül denetimi: sekme ARIA'sı, axe taraması eksikliği

- Task ID: V1-RMD-364
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-27

## Goal

Modül-modül arayüz denetim sürecinin (plan/v1/ui-audit/UI_AUDIT_PROGRESS.md) 5. modülü: PosTerminal'in
ana kasa satış ekranı (`src/Clients/PosTerminal/src/routes/Cashier.tsx`, 755 satır React/TypeScript).
On iki boyut üzerinden tarandı.

Bu dosya, önceki bağımsız denetimlerden (V1-RMD-114/286/291/314) geçmiş, WaiterPwa'ya (Modül 4) yakın
olgunlukta çıktı: eşleştirme (`pairing`) diyaloğu zaten tam bir odak-tuzağı + Escape kapatma +
tetikleyiciye odak-geri-dönüş uyguluyordu (Cashier vanilla'nın Modül 1'de sıfırdan kurduğu desenden
daha ileri düzeyde, zaten mevcuttu); geri bildirim kutuları (`toast-message`, yardım uyarıları) zaten
doğru `role="alert"`/`"status"` + `aria-live` ayrımını taşıyordu; her simge-düğme `aria-label`
taşıyordu; 401/409/ağ hatası ayrımı (`execute()`) doğru kurulmuştu.

İki gerçek bulgu:

1. Kategori rayı (`category-rail`) — Cashier vanilla'nın Modül 1'de kapattığı boşlukla (V1-RMD-360)
   birebir aynı desen — yalnızca görsel `active` sınıfı taşıyordu, `role="tab"`/`aria-selected` yoktu.
2. `Cashier.tsx` — muhtemelen tüm sistemin en yüksek trafikli ekranı — hiçbir axe-core taramasından
   geçmiyordu; halbuki `CatalogWorkspace`, `BillSplitWorkspace`, `TableWorkspace`, `FloorPlanWorkspace`,
   `KitchenOperationsWorkspace`, `ProductionShell`, dört `Online*` iş akışı ve `primitives` dahil 13
   farklı dosyada zaten "has no critical or serious axe violations" testi var. Bu, gelecekteki bir
   erişilebilirlik regresyonunun sessizce fark edilmeden kalabileceği sistemik bir test-kapsama
   boşluğuydu.

## Owned surface

- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/routes/Cashier.tsx
- `src/Clients/PosTerminal/src/routes/Cashier.category-tabs.test.tsx`
- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/v1/ui-audit/UI_AUDIT_PROGRESS.md
- `plan/v1/remediation/V1-RMD-364-posterminal-cashier-ui-audit.md`

## In scope

1. **[T1, Orta] Kategori rayı düğmeleri (`Tümü` + her kategori) bir görünüm filtresi seçiyor**
   (aynı ürün ızgarasının hangi görünümünün gösterileceğini belirliyor), ama hiçbir ARIA
   gruplama/durum taşımıyordu. `nav`'a `role="tablist"`, her düğmeye `role="tab"` +
   `aria-selected` eklendi — Modül 1'in aynı sınıftan bulgusuyla aynı desen (VIEW seçimi, Modül
   3'ün `radiogroup`/`radio` deseninden farklı olarak).
2. **[T1, Yüksek — test kapsamı] `Cashier.tsx` hiçbir axe-core taramasından geçmiyordu.**
   Kod tabanındaki 13 diğer dosyanın (`CatalogWorkspace`, `BillSplitWorkspace`, `TableWorkspace`,
   `FloorPlanWorkspace`, `KitchenOperationsWorkspace`, `ProductionShell`, 4 `Online*` iş akışı,
   `primitives`) her biri zaten kendi "has no critical or serious axe violations" testini
   taşıyor — ana kasa ekranının bu listede hiç olmaması, gelecekteki bir regresyonun test
   paketinden sessizce geçebileceği anlamına geliyordu. Aynı `axe.run(document, { rules:
   { "color-contrast": { enabled: false } } })` deseniyle yeni bir test eklendi (contrast kuralı
   diğer dosyalarla aynı gerekçeyle devre dışı: foundations.md'nin kendi AAA-doğrulanmış renk
   çiftleri zaten elle denetlendi, axe'in genel sezgisel kontrast kontrolü burada gürültü
   üretiyor).

## Out of scope

- Bu modülde ek bir ürün-katmanı (P1-P4) gözlemi bulunmadı: kategori rayı + arama + ızgara +
  adisyon paneli üçlü düzeni rakip POS ürünleriyle (Toast/Square) kıyaslanabilir basitlikte,
  öğrenme eşiği düşük (tek ekranda tüm akış).

## Dependencies

- None

## Acceptance evidence

- `src/Clients/PosTerminal`: `npx tsc --noEmit` sıfır hata; `npx vitest run` tam paketi (38 dosya,
  269 test, yeni dosyanın 2 testi dahil): 269/269 geçti, regresyon yok.
- Mutation-check: `Cashier.tsx` `git stash` ile geri alındı, yeni tablist/tab testi GERÇEKTEN
  kırmızı oldu (`role` özniteliği `null`). `git stash pop` ile geri yüklendi, paket tekrar tam
  yeşile döndü.
- Yeni axe testi, düzeltmeden önce de zaten geçiyordu (kategori rayı boşluğu axe'in kendi
  kural setinde `critical`/`serious` değil, daha düşük önem derecesinde işaretleniyor) — bu
  yüzden mutation-check'i yalnızca tablist/tab testi taşıyor; axe testi ayrı, kalıcı bir
  test-kapsama iyileştirmesi olarak eklendi.

## Handoff

- None
