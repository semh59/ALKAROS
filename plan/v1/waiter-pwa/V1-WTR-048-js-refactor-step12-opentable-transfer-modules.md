# V1-WTR-048 - JS refactor adım 12/?: openTable() taşıma ve sheets/transfer.js modülü çıkarma

- Task ID: V1-WTR-048
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

`docs/engineering/garson-refactor-plan.md`'in Bölüm 1.3'ünün kendi
notunun ve V1-WTR-043'ün kendi ertelenmiş kararının tamamlanması:
`openTable()` (+ kendi iki küçük yardımcısı `showScreen`,
`popHandoffNoteIfAny`) artık `js/screens/tables.js`'in tamamlanmış
olduğu (`bill.js` ve `menu.js` her ikisi de mevcut) `js/screens/
tables.js`'e taşındı — V1-WTR-043'ün kendi dosya başı yorumunun tam
olarak beklediği an. Bunun mümkün olması `js/sheets/transfer.js`'in
çıkarılabilmesinin de önkoşuluydu: `confirmTransfer` `openTable`'ı
çağırıyor, o da artık kendi modülünde.

Bu adımda gerçek bir dairesel import riski bulundu ve önlendi:
`tables.js`'in `bill.js`'den (`activeItems`, `loadOrder`, `renderBill`,
vb.) içe aktarması gerekiyordu, ama `bill.js` da `seatLabel`'i
`tables.js`'ten alıyordu — `tables.js → bill.js → tables.js` gerçek bir
döngü olurdu. Çözüm: `seatLabel` (yalnız `state.tableSeats`'e bağımlı,
saf bir türetilmiş-durum okuyucusu) `js/state.js`'e taşındı —
`persistDraftsByTable`/`loadDraftsByTable`'ın yanına, aynı "paylaşılan
durumu okuyan küçük fonksiyon" ailesine. Döngü hiç oluşmadı.

## Owned surface

- `src/Clients/WaiterPwa/wwwroot/js/sheets/transfer.js` (yeni).
- Sınırlı ek (V1-WTR-010 ailesinin sahipliğinde kalır):
  - src/Clients/WaiterPwa/wwwroot/js/screens/tables.js — `showScreen`,
    `openTable`, `popHandoffNoteIfAny` eklendi (V1-WTR-043'ün
    sahipliğinde kalır, bu görev yalnız ekliyor).
  - src/Clients/WaiterPwa/wwwroot/js/state.js — `seatLabel` eklendi
    (V1-WTR-038'in sahipliğinde kalır, bu görev yalnız ekliyor).
  - src/Clients/WaiterPwa/wwwroot/js/sheets/bill.js — `seatLabel`
    import'u `../screens/tables.js`'ten `../state.js`'e değişti
    (V1-WTR-045'in sahipliğinde kalır, dairesel import'u önlemek için).
  - src/Clients/WaiterPwa/wwwroot/waiter-app.js — üç fonksiyon +
    dört transfer fonksiyonu silindi, `import` listesine iki satır
    eklendi.
  - src/Clients/WaiterPwa/wwwroot/sw.js — `CACHE_NAME` v14'ten v15'e
    yükseltildi; `ASSETS_TO_CACHE`'e `./js/sheets/transfer.js` eklendi.

## Out of scope

- Kalan sheet modülleri (pending-orders, failed-orders, profile) ve en
  riskli ikisi (`offline-queue.js`, `push.js`) — ayrı görevler.

## Dependencies

- V1-WTR-046
- V1-WTR-047

## Acceptance evidence

- `node --check` beş dosyanın da (`js/screens/tables.js`,
  `js/sheets/transfer.js`, `js/sheets/bill.js`, `js/state.js`,
  `waiter-app.js`) sözdizimsel olarak geçerli olduğunu doğruladı. Her
  `import` edilen ismin gerçekten kullanıldığı `grep`'le tek tek
  kontrol edildi (V1-WTR-046'nın kendi eksik-import hatasından
  çıkarılan disiplin, bu adımda da uygulandı).
- `npx playwright test` (`tests/E2E/WaiterPwa`, gerçek Chromium + gerçek
  Postgres + gerçek Host ikili dosyası): **3/3 çalıştırmada 18/18
  temiz** (~43s/çalıştırma, `openTable()`'ın taşınması ve dairesel
  import riskinin çözümü uygulamanın en temel akışına dokunduğu için
  üç kez) — paketteki HER test en az bir masa açıyor, bu modülün
  gerçek kapsamı geniş şekilde doğrulandı.
- `grep -n "^  function showScreen\|^  async function openTable\|^
  async function popHandoffNoteIfAny\|^  function openTransferSheet\|
  ^  async function confirmTransfer\b\|^  async function
  openTransferServerSheet\|^  async function confirmTransferServer"`
  `waiter-app.js`'te → 0 eşleşme (taşındı, kopyalanmadı).
- `python tools/consistency-audit/consistency_audit.py` → bu görevin
  değiştirdiği dosyalarda 0 ihlal. Kalan tek ihlal,
  `src/Modules/Inventory/ManualAdjustments/InventoryAdjustmentService.cs:96`,
  bu görevden önce vardı ve sahip olunan yüzeyin dışında.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0
  uyarı.

## Handoff

- None
