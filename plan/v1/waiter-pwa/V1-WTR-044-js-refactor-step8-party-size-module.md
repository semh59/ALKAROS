# V1-WTR-044 - JS refactor adım 8/?: sheets/party-size.js modülü çıkarma

- Task ID: V1-WTR-044
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

`docs/engineering/garson-refactor-plan.md`'in Bölüm 2.4'ündeki JS
göç stratejisinin devamı, planın kendi sıralamasındaki **ikinci sheet
modülü**: `openPartySizeSheet`/`partySizeSheetHtml`
`js/sheets/party-size.js`'e taşındı.

`confirmPartySize` bilinçli olarak taşınmadı — `renderBill()`'e bağımlı,
o da `bill.js`'in kendisi henüz modül olmadığı için burada kalıyor
(planın kendi sıralaması: tables.js → party-size.js → **bill.js** →
menu.js/product-sheet.js). Kuver stepper'ının (+/-) tıklama işleyicisi
de `onOptionsBodyClick`'in paylaşılan sheet-yönlendirme anahtarının
parçası, o da bu adımın kapsamı değil.

## Owned surface

- `src/Clients/WaiterPwa/wwwroot/js/sheets/party-size.js` (yeni).
- Sınırlı ek (V1-WTR-010 ailesinin sahipliğinde kalır):
  - src/Clients/WaiterPwa/wwwroot/waiter-app.js — iki fonksiyon
    silindi, `import` listesine bir satır eklendi.
  - src/Clients/WaiterPwa/wwwroot/sw.js — `CACHE_NAME` v10'dan v11'e
    yükseltildi; `ASSETS_TO_CACHE`'e `./js/sheets/party-size.js`
    eklendi.

## Out of scope

- `confirmPartySize` — yukarıda gerekçelendi, `bill.js` hazır olunca
  taşınacak.
- Planın kalan adımları (`bill.js`, `menu.js`/`product-sheet.js`, kalan
  sheet modülleri, `offline-queue.js`, `push.js`).

## Dependencies

- V1-WTR-041
- V1-WTR-043

## Acceptance evidence

- `node --check` her iki dosyanın (`js/sheets/party-size.js`,
  `waiter-app.js`) da sözdizimsel olarak geçerli olduğunu doğruladı.
- `npx playwright test` (`tests/E2E/WaiterPwa`, gerçek Chromium + gerçek
  Postgres + gerçek Host ikili dosyası): **2/2 çalıştırmada 18/18
  temiz** (~43s/çalıştırma) — `02-ordering.spec.js`'in ilk adımı
  ("1. masa açılır ve kişi sayısı ilk turdan önce ayarlanır") bu
  modülün gerçek kapsamını doğrudan doğruluyor.
- `grep -n "^  function openPartySizeSheet\|^  function
  partySizeSheetHtml"` `waiter-app.js`'te → 0 eşleşme (taşındı,
  kopyalanmadı).
- `python tools/consistency-audit/consistency_audit.py` → bu görevin
  değiştirdiği dosyalarda 0 ihlal. Kalan tek ihlal,
  `src/Modules/Inventory/ManualAdjustments/InventoryAdjustmentService.cs:96`,
  bu görevden önce vardı ve sahip olunan yüzeyin dışında.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0
  uyarı.

## Handoff

- None
