# V1-WTR-041 - JS refactor adım 5/?: options-sheet.js modülü çıkarma

- Task ID: V1-WTR-041
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

`docs/engineering/garson-refactor-plan.md`'in Bölüm 2.4'ündeki JS
göç stratejisinin devamı: `openOptions()`/`closeOptions()` (ve
`lastOptionsFocus` durumu) `js/options-sheet.js` adlı native ES
modülüne taşındı.

Planın kendi dosya listesinde ayrı bir "options-sheet" modülü sayılmıyor,
ama kod incelemesi bunun `sheets/` altındaki HER modülün (void-comp,
transfer, help-request, party-size, pending-orders, failed-orders,
profile, PIN kilidi) üzerine kurulduğu tek, genel amaçlı sheet altyapısı
olduğunu gösterdi. Bu ikisini önce çıkarmak, planın kendi listesindeki
sekiz sheet modülünün her birinin `waiter-app.js`'e değil bu modüle
bağımlı olmasını sağlıyor — sıra bilinçli olarak PIN kilidinden önce
buraya geldi (PIN kilidi de bu altyapıya bağımlı).

## Owned surface

- `src/Clients/WaiterPwa/wwwroot/js/options-sheet.js` (yeni).
- Sınırlı ek (V1-WTR-010 ailesinin sahipliğinde kalır):
  - src/Clients/WaiterPwa/wwwroot/waiter-app.js — `openOptions()`,
    `closeOptions()`, `lastOptionsFocus` silindi, `import` listesine
    bir satır eklendi.
  - src/Clients/WaiterPwa/wwwroot/sw.js — `CACHE_NAME` v7'den v8'e
    yükseltildi; `ASSETS_TO_CACHE`'e `./js/options-sheet.js` eklendi.

## Out of scope

- PIN kilidi, ekran modülleri (`tables.js`, `menu.js`, `bill.js`,
  `product-sheet.js`), sekiz sheet modülü, `offline-queue.js`,
  `push.js` — ayrı görevler.
- `onOptionsBodyClick`/`onOptionsConfirm` — sheet türüne göre dağılan
  büyük yönlendirme mantığı, her sheet modülü çıkarılırken kendi
  parçasına bölünecek; bu adımın kapsamı değil.

## Dependencies

- V1-WTR-039

## Acceptance evidence

- `node --check` her iki dosyanın (`js/options-sheet.js`,
  `waiter-app.js`) da sözdizimsel olarak geçerli olduğunu doğruladı.
- `npx playwright test` (`tests/E2E/WaiterPwa`, gerçek Chromium + gerçek
  Postgres + gerçek Host ikili dosyası): **2/2 çalıştırmada 18/18
  temiz** (~44s/çalıştırma) — paketteki neredeyse her test en az bir
  sheet açıyor, bu yüzden bu modülün gerçek kapsamı geniş şekilde
  doğrulandı.
- `grep -n "lastOptionsFocus"` `waiter-app.js`'te → 0 eşleşme (taşındı,
  kopyalanmadı).
- `python tools/consistency-audit/consistency_audit.py` → bu görevin
  değiştirdiği dosyalarda 0 ihlal. Kalan tek ihlal,
  `src/Modules/Inventory/ManualAdjustments/InventoryAdjustmentService.cs:96`,
  bu görevden önce vardı ve sahip olunan yüzeyin dışında.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0
  uyarı.

## Handoff

- None
