# V1-WTR-037 - JS refactor adım 1/?: util.js modülü çıkarma

- Task ID: V1-WTR-037
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

`docs/engineering/garson-refactor-plan.md`'in Bölüm 2.4'ündeki JS
göç stratejisinin **ilk adımı**: `waiter-app.js`'in hiçbir paylaşılan
duruma dokunmayan saf fonksiyonları `js/util.js` adlı native ES
modülüne taşındı. `index.html`'deki `<script>` etiketi `type="module"`
oldu (planın öngördüğü, tek satırlık, geri kalan modül grafiğini çözen
değişiklik).

Planın kendi dosya listesinden bilinçli bir sapma: liste `seatLabel`
(`state.tableSeats`'e bağımlı) ve `measureChrome` (`el.ribbon`/
`el.pendingBanner`'a bağımlı) fonksiyonlarını da util.js'e koyuyordu,
ama kod incelemesi ikisinin de paylaşılan durum/DOM önbelleğine bağımlı
olduğunu gösterdi — plan'ın kendi kriteri ("hiçbir paylaşılan duruma
dokunmuyorlar") bunları dışlıyor. İkisi de bir sonraki adıma
(`state.js`/`screens/tables.js`) bırakıldı.

## Owned surface

- `src/Clients/WaiterPwa/wwwroot/js/util.js` (yeni) —
  `escapeHtml`, `formatMoney`, `formatQuantity`, `formatClock`,
  `describeHttpFailure`, `randomUUID`, `deviceTerminalId`,
  `courseLabel`, `isFullscreen`.
- Sınırlı ek (V1-WTR-010 ailesinin sahipliğinde kalır):
  - src/Clients/WaiterPwa/wwwroot/waiter-app.js — yukarıdaki dokuz
    fonksiyon silindi, dosyanın en üstüne bir `import` eklendi; davranış
    değişmedi.
  - src/Clients/WaiterPwa/wwwroot/index.html — `<script src=
    "./waiter-app.js">` → `<script type="module" src="./waiter-app.js">`.
  - src/Clients/WaiterPwa/wwwroot/sw.js — `CACHE_NAME` v3'ten v4'e
    yükseltildi (yeni bir modül dosyası önbelleğe eklenince eski bir
    cihazın eski önbelleği kalması gerekmiyor); `ASSETS_TO_CACHE`'e
    `./js/util.js` eklendi.

## Out of scope

- Planın kalan adımları (`state.js`, `api.js`, `auth.js`, ekran/sheet
  modülleri, `offline-queue.js`, `push.js`) — ayrı görevler, planın
  kendi 2.4'te belirttiği sırayla.
- `seatLabel`/`measureChrome` — yukarıda gerekçelendirildiği gibi bu
  adıma dahil değil.

## Dependencies

- None

## Acceptance evidence

- `node --check src/Clients/WaiterPwa/wwwroot/js/util.js` ve
  `waiter-app.js` → ikisi de sözdizimsel olarak geçerli.
- `node` ile `js/util.js`'in izole içe aktarımı: dokuz export'un hepsi
  mevcut, `formatMoney`/`escapeHtml`/`courseLabel` gerçek girdilerle
  doğru çıktı üretti (`isFullscreen` `document` gerektirdiği için
  Node'da beklenen şekilde hata verdi — tarayıcı dışı bir ortamda
  normal, gerçek doğrulama E2E paketinden geçti).
- `npx playwright test` (`tests/E2E/WaiterPwa`, gerçek Chromium + gerçek
  Postgres + gerçek Host ikili dosyası): **2/2 çalıştırmada 18/18
  temiz** (~43s/çalıştırma) — modül grafiğinin gerçek tarayıcıda
  sorunsuz yüklendiğinin ve hiçbir davranış regresyonu olmadığının
  kanıtı.
- `grep -n "function escapeHtml\|function formatMoney\|function
  formatQuantity\|function formatClock\|function describeHttpFailure\|
  function randomUUID\|function deviceTerminalId\|function courseLabel\|
  function isFullscreen" waiter-app.js` → 0 eşleşme (fonksiyonlar
  TAŞINDI, kopyalanmadı — planın 2.4'ün kendi uyarısı).
- `python tools/consistency-audit/consistency_audit.py` → bu görevin
  değiştirdiği dosyalarda 0 ihlal. Kalan tek ihlal,
  `src/Modules/Inventory/ManualAdjustments/InventoryAdjustmentService.cs:96`,
  bu görevden önce vardı ve sahip olunan yüzeyin dışında.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0
  uyarı.

## Handoff

- None
