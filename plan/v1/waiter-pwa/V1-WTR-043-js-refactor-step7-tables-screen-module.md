# V1-WTR-043 - JS refactor adım 7/?: screens/tables.js modülü çıkarma

- Task ID: V1-WTR-043
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

`docs/engineering/garson-refactor-plan.md`'in Bölüm 2.4'ündeki JS
göç stratejisinin devamı, planın kendi sıralamasındaki **ilk ekran
modülü**: `loadZones`, `loadTables`, `loadTableSeats`, `renderZones`,
`renderTables` (ve onların özel yardımcıları `tableAgeMinutes`/
`tableAgeBadgeHtml`/`TABLE_STATUS`) `js/screens/tables.js`'e taşındı.

Planın kendi listesinden bilinçli bir sapma: `openTable()` bu adıma dahil
edilmedi. Kod incelemesi bunun masalar/hesap/menü arasında orkestrasyon
yaptığını gösterdi (`closeBill`/`openBill`, `showScreen`, `renderProducts`,
`renderBill`, `persistDraftsByTable`, `popHandoffNoteIfAny`) — ne
`bill.js` ne `menu.js` henüz modül olarak var, bu yüzden `openTable`'ı
şimdi taşımak bu modülün `waiter-app.js`'e geri dönen bir import
yapmasını gerektirirdi. Planın kendi 2.4 sıralaması (tables.js →
party-size.js → bill.js → menu.js/product-sheet.js → geri kalanı) tam
olarak bu yüzden var; `openTable` bill.js/menu.js hazır olunca kendi
gerçek yerine taşınacak.

`TABLE_STATUS` Türkçe sözlüğü de bu adımda taşındı — tek okuyucusu
`renderTables` olduğu doğrulandı (`grep`), ayrı bir `dictionaries.js`
gerektirmeden doğrudan modülün içine taşınabildi.

## Owned surface

- `src/Clients/WaiterPwa/wwwroot/js/screens/tables.js` (yeni).
- Sınırlı ek (V1-WTR-010 ailesinin sahipliğinde kalır):
  - src/Clients/WaiterPwa/wwwroot/waiter-app.js — beş fonksiyon + iki
    yardımcı + iki sabit + `TABLE_STATUS` silindi, `import` listesine
    bir satır eklendi.
  - src/Clients/WaiterPwa/wwwroot/sw.js — `CACHE_NAME` v9'dan v10'a
    yükseltildi; `ASSETS_TO_CACHE`'e `./js/screens/tables.js` eklendi.

## Out of scope

- `openTable()` — yukarıda gerekçelendi, `bill.js`/`menu.js` hazır
  olunca taşınacak.
- Planın kalan adımları (`party-size.js`, `bill.js`, `menu.js`/
  `product-sheet.js`, kalan sheet modülleri, `offline-queue.js`,
  `push.js`).

## Dependencies

- V1-WTR-039
- V1-WTR-040

## Acceptance evidence

- `node --check` her iki dosyanın (`js/screens/tables.js`,
  `waiter-app.js`) da sözdizimsel olarak geçerli olduğunu doğruladı.
- `npx playwright test` (`tests/E2E/WaiterPwa`, gerçek Chromium + gerçek
  Postgres + gerçek Host ikili dosyası): **2/2 çalıştırmada 18/18
  temiz** (~44s/çalıştırma) — paketteki her test en az bir masa açıyor,
  bu modülün gerçek kapsamı geniş şekilde doğrulandı.
- `grep -n "function renderZones\|const TABLE_AGE_WARNING_MINUTES\|
  function tableAgeMinutes\|function tableAgeBadgeHtml\|function
  renderTables\|async function loadZones\|async function loadTables\|
  async function loadTableSeats\|TABLE_STATUS\b"` `waiter-app.js`'te →
  yalnız bir yorum satırı eşleşiyor (gerçek tanım yok — taşındı,
  kopyalanmadı).
- `python tools/consistency-audit/consistency_audit.py` → bu görevin
  değiştirdiği dosyalarda 0 ihlal. Kalan tek ihlal,
  `src/Modules/Inventory/ManualAdjustments/InventoryAdjustmentService.cs:96`,
  bu görevden önce vardı ve sahip olunan yüzeyin dışında.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0
  uyarı.

## Handoff

- None
