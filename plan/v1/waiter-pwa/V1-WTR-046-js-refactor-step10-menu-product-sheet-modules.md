# V1-WTR-046 - JS refactor adım 10/?: screens/menu.js ve sheets/product-sheet.js modülleri çıkarma

- Task ID: V1-WTR-046
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

`docs/engineering/garson-refactor-plan.md`'in Bölüm 2.4'ündeki JS
göç stratejisinin devamı, planın kendi sıralamasındaki **dördüncü ekran
modülü**, planın kendi notuna göre birlikte: `renderCategories`,
`renderProducts`, `addToDraft`, `draftQuantityOf`, `afterDraftChange`
`js/screens/menu.js`'e; `openProductSheet`, `productSheetHtml`,
`chosenModifiers`, `toggleModifier`, `updateProductSheetTotal`
`js/sheets/product-sheet.js`'e taşındı.

`persistDraftsByTable` (`state.draftsByTable`'ın yazma tarafı) bilinçli
olarak `js/state.js`'e taşındı — planın kendi metni bunu `offline-
queue.js`'e ait sayıyordu, ama `afterDraftChange` buna bağımlı ve
`offline-queue.js` henüz yok; `state.js` zaten okuma tarafını
(`loadDraftsByTable`) barındırıyordu, ikisi yeniden bir arada.

Ortak tıklama/onay yönlendiricileri (`onOptionsBodyClick`,
`onOptionsConfirm`) bilinçli olarak taşınmadı — henüz kendi modülü
olmayan hemen her sheet'e dağılan büyük bir `switch` mantığı; "ürün"
dalları `toggleModifier`/`updateProductSheetTotal`/`addToDraft`'ı
`waiter-app.js`'in kendisinin diğer modüllerden çektiği gibi çekiyor.

## Owned surface

- PO:2026-09-16 kararıyla src/Clients/WaiterPwa/wwwroot/js/screens/menu.js
  yüzeyi kalan-adet rozeti için V1-WTR-055'e devredildi; bu historical task
  closed kalır.
- `src/Clients/WaiterPwa/wwwroot/js/sheets/product-sheet.js` (yeni).
- Sınırlı ek (V1-WTR-010 ailesinin sahipliğinde kalır):
  - src/Clients/WaiterPwa/wwwroot/js/state.js — `persistDraftsByTable`
    eklendi (V1-WTR-038'in sahipliğinde kalır, bu görev yalnız ekliyor).
  - src/Clients/WaiterPwa/wwwroot/waiter-app.js — on bir fonksiyon
    silindi (biri, `toggleModifier`, iki farklı yerde tanımlıydı — ikisi
    de silindi), `import` listesine üç satır eklendi (biri düzeltme
    olarak, yukarıya bkz).
  - src/Clients/WaiterPwa/wwwroot/sw.js — `CACHE_NAME` v12'den v13'e
    yükseltildi; `ASSETS_TO_CACHE`'e iki yeni dosya eklendi.

## Out of scope

- `onOptionsBodyClick`/`onOptionsConfirm` — yukarıda gerekçelendi.
- Kalan sheet modülleri (`offline-queue.js`, `push.js` dahil) — ayrı
  görevler.

## Dependencies

- V1-WTR-045

## Acceptance evidence

Bu görevde bulunan ve düzeltilen gerçek bir hata (kanıtın parçası):
`persistDraftsByTable`'ı `waiter-app.js`'ten silip `state.js`'e
taşırken, `waiter-app.js`'in kendi `import { state, el } from
'./js/state.js'` satırına `persistDraftsByTable`'ı eklemeyi UNUTTUM —
`openTable()` bu fonksiyonu hâlâ çağırıyordu (satır 391), artık tanımsız
bir isimdi. `node --check` bunu yakalamadı (geçerli sözdizimi, yalnız
çalışma zamanında patlayan bir `ReferenceError`) — tam olarak bu
refactor'un E2E paketine bu kadar sıkı bağlı olmasının nedeni. Gerçek
sonuç: masaya her dokunuşta `openTable()` `showScreen('menu')`'a hiç
ulaşamadan patlıyordu, menü ekranına asla geçilmiyordu — E2E paketi
3/3 çalıştırmada `#productList [data-product]` görünür olmadığı için
5 testte başarısız oldu (`02-ordering`, `03-waiter-actions`,
`04-performance`, `05-load-and-timing`'in ikisi). `import` satırına
eksik ismi ekleyerek düzeltildi, 3/3 tekrar çalıştırmada 18/18 temiz.

- `node --check` beş dosyanın da (`js/screens/menu.js`,
  `js/sheets/product-sheet.js`, `js/state.js`, `waiter-app.js`) sözdizimsel
  olarak geçerli olduğunu doğruladı — ama yukarıdaki gerçek hatayı
  yakalamadı, onu yalnız E2E paketi yakaladı.
- `npx playwright test` (`tests/E2E/WaiterPwa`, gerçek Chromium + gerçek
  Postgres + gerçek Host ikili dosyası): düzeltmeden ÖNCE **3/3
  çalıştırmada 5 test başarısız** (yukarıya bkz); düzeltmeden SONRA
  **3/3 çalıştırmada 18/18 temiz** (~43s/çalıştırma, en büyük ve en
  riskli adım olduğu için üç kez).
- `grep -n "^  function renderCategories\|^  function draftQuantityOf\|
  ^  function renderProducts\|^  function addToDraft\|^  function
  afterDraftChange\|^  function openProductSheet\|^  function
  productSheetHtml\|^  function chosenModifiers\|^  function
  updateProductSheetTotal\|^  function toggleModifier\|^  function
  persistDraftsByTable"` `waiter-app.js`'te → 0 eşleşme (taşındı,
  kopyalanmadı).
- `python tools/consistency-audit/consistency_audit.py` → bu görevin
  değiştirdiği dosyalarda 0 ihlal. Kalan tek ihlal,
  `src/Modules/Inventory/ManualAdjustments/InventoryAdjustmentService.cs:96`,
  bu görevden önce vardı ve sahip olunan yüzeyin dışında.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0
  uyarı.

## Handoff

- None
