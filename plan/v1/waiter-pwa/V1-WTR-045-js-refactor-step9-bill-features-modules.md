# V1-WTR-045 - JS refactor adım 9/?: sheets/bill.js ve features.js modülleri çıkarma

- Task ID: V1-WTR-045
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

`docs/engineering/garson-refactor-plan.md`'in Bölüm 2.4'ündeki JS
göç stratejisinin devamı, planın kendi sıralamasındaki **üçüncü ekran
modülü**: hesabın kendisi — `activeItems`, `modifierCountFor`,
`lineExtras`, `draftTotal`, `lastRound`, `renderQuickSend`, `renderBill`,
`renderDraftLine`, `renderSentLine`, `openBill`, `closeBill` (ve
`KITCHEN_STATE` Türkçe sözlüğü) — `js/sheets/bill.js`'e taşındı.

Planın kendi listesinde açıkça sayılmayan `js/features.js` da bu adımda
çıkarıldı (`loadFeatures`, `featureEnabled`): `renderBill` bu ikisine
bağımlı ve ikisi de yalnızca `state.features`'a dokunan, ayrı, küçük bir
kaygı — `seatLabel` de aynı gerekçeyle bir önceki adımda `tables.js`'e
eklenmişti.

`activeItems` bilinçli olarak burada kaldı, planın kendi listesinde
`bill.js`'e ait sayılmasa da: henüz modül olmayan diğer sheet'lerin
(void, void-sent, comp, transfer) hepsi bunu çağırıyor — hepsi "hesaptaki
aktif kalemler" sorusunu soruyor, `bill.js`'in kendi sorularının aynısı.
Onlar kendi modüllerine taşınınca `activeItems`'ı buradan alacaklar; bu,
refactor'un bu oturumdaki her adımında korunan tek yönlü bağımlılık
kuralının (henüz taşınmamış bir parçanın zaten taşınmış bir modülden
import etmesi sorun değil, tersi sorun) doğal bir uzantısı.

`afterDraftChange` bilinçli olarak taşınmadı — `renderProducts()`'a
bağımlı, o da `menu.js`'in kendisi henüz modül olmadığı için
`waiter-app.js`'te kalıyor.

## Owned surface

- `src/Clients/WaiterPwa/wwwroot/js/sheets/bill.js` (yeni).
- `src/Clients/WaiterPwa/wwwroot/js/features.js` (yeni).
- Sınırlı ek (V1-WTR-010 ailesinin sahipliğinde kalır):
  - src/Clients/WaiterPwa/wwwroot/js/screens/tables.js — `seatLabel`
    eklendi (V1-WTR-043'ün sahipliğinde kalır, bu görev yalnız ekliyor).
  - src/Clients/WaiterPwa/wwwroot/waiter-app.js — on bir fonksiyon +
    `KITCHEN_STATE` + `seatLabel` silindi, `import` listesine iki satır
    eklendi, kullanılmayan `courseLabel` import'u kaldırıldı.
  - src/Clients/WaiterPwa/wwwroot/sw.js — `CACHE_NAME` v11'den v12'ye
    yükseltildi; `ASSETS_TO_CACHE`'e `./js/features.js` ve
    `./js/sheets/bill.js` eklendi.

## Out of scope

- `afterDraftChange` — yukarıda gerekçelendi, `menu.js` hazır olunca
  taşınacak.
- `menu.js`/`product-sheet.js`, kalan sheet modülleri
  (`offline-queue.js`, `push.js` dahil) — ayrı görevler.

## Dependencies

- V1-WTR-043

## Acceptance evidence

- `node --check` dört dosyanın da (`js/sheets/bill.js`, `js/features.js`,
  `js/screens/tables.js`, `waiter-app.js`) sözdizimsel olarak geçerli
  olduğunu doğruladı.
- `npx playwright test` (`tests/E2E/WaiterPwa`, gerçek Chromium + gerçek
  Postgres + gerçek Host ikili dosyası): **3/3 çalıştırmada 18/18
  temiz** (~43s/çalıştırma, uygulamanın en çok kullanılan ekranı
  olduğu için üç kez) — paketteki HER test bu modülü doğrudan egzersiz
  ediyor.
- `grep -n "^  function activeItems\|^  function modifierCountFor\|^
  function lineExtras\|^  function draftTotal\|^  function lastRound\|
  ^  function renderQuickSend\|^  function renderBill\|^  function
  renderDraftLine\|^  function renderSentLine\|^  function openBill\|
  ^  function closeBill\|^  function seatLabel\|^  async function
  loadFeatures\|^  function featureEnabled\|^  const KITCHEN_STATE"`
  `waiter-app.js`'te → 0 eşleşme (taşındı, kopyalanmadı).
- `python tools/consistency-audit/consistency_audit.py` → bu görevin
  değiştirdiği dosyalarda 0 ihlal. Kalan tek ihlal,
  `src/Modules/Inventory/ManualAdjustments/InventoryAdjustmentService.cs:96`,
  bu görevden önce vardı ve sahip olunan yüzeyin dışında.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0
  uyarı.

## Handoff

- None
