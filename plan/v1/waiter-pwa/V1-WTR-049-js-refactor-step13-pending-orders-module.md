# V1-WTR-049 - JS refactor adım 13/?: sheets/pending-orders.js modülü çıkarma

- Task ID: V1-WTR-049
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

`docs/engineering/garson-refactor-plan.md`'in Bölüm 2.4'ündeki JS
göç stratejisinin devamı: `loadPending`/`renderPendingBanner`/
`openPendingSheet`/`resolvePendingGuarded`/`resolvePending` (misafir QR
siparişlerinin onay akışı) ve `openSendToCashierSheet`/
`confirmSendToCashier` (hesabı kasaya gönderme) `js/sheets/
pending-orders.js`'e taşındı — planın kendi listesinde bu iki ayrı
sheet sayılıyordu, ama ikisi de aynı küçük dosyada birleştirildi
(ikisi de "bir siparişi bir sonraki duruma taşı" sorusunu soruyor).

`measureChrome` de bu adımda `js/state.js`'e taşındı (V1-WTR-037'nin
util.js'ten bilinçli olarak dışarıda bıraktığı fonksiyon) —
`renderPendingBanner` buna bağımlı ve artık iki farklı modülün
(`waiter-app.js`'in kendi `renderRibbon`'ı ve bu dosyanın
`renderPendingBanner`'ı) ihtiyacı var; `el`-only bir okuyucu olduğu
için `state.js`'teki `seatLabel`/`persistDraftsByTable` ailesine katıldı.

`fireCourse` bilinçli olarak taşınmadı — kendi başına küçük ve
bağımsız, ayrı bir görevde (muhtemelen `bill.js`'e, kursu ateşleyen
buton `renderSentLine`'da render ediliyor) değerlendirilebilir; bu
adımın kapsamı değil.

## Owned surface

- `src/Clients/WaiterPwa/wwwroot/js/sheets/pending-orders.js` (yeni).
- Sınırlı ek (V1-WTR-010 ailesinin sahipliğinde kalır):
  - src/Clients/WaiterPwa/wwwroot/js/state.js — `measureChrome`
    eklendi (V1-WTR-038'in sahipliğinde kalır, bu görev yalnız ekliyor).
  - src/Clients/WaiterPwa/wwwroot/waiter-app.js — yedi fonksiyon
    silindi, `import` listesine iki satır eklendi (biri `state.js`'in
    mevcut import'una `measureChrome` ekliyor).
  - src/Clients/WaiterPwa/wwwroot/sw.js — `CACHE_NAME` v15'ten v16'ya
    yükseltildi; `ASSETS_TO_CACHE`'e `./js/sheets/pending-orders.js`
    eklendi.

## Out of scope

- `fireCourse` — yukarıda gerekçelendi.
- Kalan sheet modülleri (failed-orders, profile) ve en riskli ikisi
  (`offline-queue.js`, `push.js`) — ayrı görevler.

## Dependencies

- V1-WTR-046
- V1-WTR-048

## Acceptance evidence

- `node --check` üç dosyanın da (`js/sheets/pending-orders.js`,
  `js/state.js`, `waiter-app.js`) sözdizimsel olarak geçerli olduğunu
  doğruladı. Her import edilen ismin gerçekten kullanıldığı (bare
  event-listener referansları dahil) tek tek `grep`'le kontrol edildi.
- `npx playwright test` (`tests/E2E/WaiterPwa`, gerçek Chromium + gerçek
  Postgres + gerçek Host ikili dosyası): **2/2 çalıştırmada 18/18
  temiz** (~43s/çalıştırma).
- `grep -n "^  function renderPendingBanner\|^  function
  openPendingSheet\|^  async function resolvePendingGuarded\|^  async
  function resolvePending\b\|^  function openSendToCashierSheet\|^
  async function confirmSendToCashier\|^  async function loadPending\|
  ^  function measureChrome"` `waiter-app.js`'te → 0 eşleşme (taşındı,
  kopyalanmadı).
- `python tools/consistency-audit/consistency_audit.py` → bu görevin
  değiştirdiği dosyalarda 0 ihlal. Kalan tek ihlal,
  `src/Modules/Inventory/ManualAdjustments/InventoryAdjustmentService.cs:96`,
  bu görevden önce vardı ve sahip olunan yüzeyin dışında.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0
  uyarı.

## Handoff

- None
