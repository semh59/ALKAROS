# V1-WTR-050 - JS refactor adım 14/?: sheets/failed-orders.js modülü çıkarma

- Task ID: V1-WTR-050
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

`docs/engineering/garson-refactor-plan.md`'in Bölüm 2.4'ündeki JS
göç stratejisinin devamı: `openFailedOrdersSheet`/`queuedOrderRow`
`js/sheets/failed-orders.js`'e taşındı.

`dismissFailedOrder`/`clearAllFailedOrders` bilinçli olarak taşınmadı —
ikisi de `persistQueue()`'ya bağımlı, o da `offline-queue.js`'in kendisi
henüz modül olmadığı için `waiter-app.js`'te kalıyor. Bu, V1-WTR-044'ün
(`party-size.js`) aç/onayla ayrımıyla aynı desen — bir sheet'in "aç ve
göster" yarısı hazır bağımlılıklarla şimdi taşınabiliyor, "mutasyon"
yarısı henüz hazır olmayan bir bağımlılık nedeniyle bekliyor.

## Owned surface

- `src/Clients/WaiterPwa/wwwroot/js/sheets/failed-orders.js` (yeni).
- Sınırlı ek (V1-WTR-010 ailesinin sahipliğinde kalır):
  - src/Clients/WaiterPwa/wwwroot/waiter-app.js — iki fonksiyon silindi,
    `import` listesine bir satır eklendi; artık kullanılmayan
    `formatQuantity` import'u kaldırıldı (yalnız bu iki fonksiyon
    kullanıyordu).
  - src/Clients/WaiterPwa/wwwroot/sw.js — `CACHE_NAME` v16'dan v17'ye
    yükseltildi; `ASSETS_TO_CACHE`'e `./js/sheets/failed-orders.js`
    eklendi.

## Out of scope

- `dismissFailedOrder`/`clearAllFailedOrders` — yukarıda gerekçelendi.
- `sheets/profile.js` ve en riskli ikisi (`offline-queue.js`,
  `push.js`) — ayrı görevler.

## Dependencies

- V1-WTR-041

## Acceptance evidence

- `node --check` her iki dosyanın (`js/sheets/failed-orders.js`,
  `waiter-app.js`) da sözdizimsel olarak geçerli olduğunu doğruladı.
  Kalan her `util.js` import'unun (`escapeHtml`, `formatMoney`,
  `randomUUID`, `isFullscreen`) hâlâ gerçekten kullanıldığı tek tek
  `grep`'le doğrulandı (bir önceki bağımsız incelemenin bulduğu
  kullanılmayan-import sınıfı hatasını tekrarlamamak için).
- `npx playwright test` (`tests/E2E/WaiterPwa`, gerçek Chromium + gerçek
  Postgres + gerçek Host ikili dosyası): **2/2 çalıştırmada 18/18
  temiz** (~43s/çalıştırma).
- `grep -n "^  function openFailedOrdersSheet\|^  function
  queuedOrderRow"` `waiter-app.js`'te → 0 eşleşme (taşındı,
  kopyalanmadı).
- `python tools/consistency-audit/consistency_audit.py` → bu görevin
  değiştirdiği dosyalarda 0 ihlal. Kalan tek ihlal,
  `src/Modules/Inventory/ManualAdjustments/InventoryAdjustmentService.cs:96`,
  bu görevden önce vardı ve sahip olunan yüzeyin dışında.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0
  uyarı.

## Handoff

- None
