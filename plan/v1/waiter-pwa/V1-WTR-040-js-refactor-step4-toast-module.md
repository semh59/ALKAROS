# V1-WTR-040 - JS refactor adım 4/?: toast.js modülü çıkarma

- Task ID: V1-WTR-040
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

`docs/engineering/garson-refactor-plan.md`'in Bölüm 2.4'ündeki JS
göç stratejisinin devamı: `toast()` (ve onun `MAX_VISIBLE_TOASTS` sabiti)
`js/toast.js` adlı native ES modülüne taşındı.

Planın kendi dosya listesinde `toast()` açıkça sayılmıyor, ama kod
incelemesi bunun tamamen bağımsız (yalnız `el.toasts` ve `escapeHtml`'e
bağımlı) ve gelecekteki HER ekran/sheet modülünün ihtiyaç duyacağı bir
yaprak (leaf) modül olduğunu gösterdi. PIN kilidi (planın auth.js
listesinden V1-WTR-039'da bilinçli olarak dışarıda bırakılan kısım)
`toast()`'a, henüz çıkarılmamış `openOptions()`/`closeOptions()`'a
bağımlı — bu üçünün hepsi hazır olmadan kilit ekranını tek başına
çıkarmak, `waiter-app.js`'e geri dönen bir dairesel import
gerektirirdi. Bu yüzden sıra `toast.js`'ten önce PIN kilidine değil,
önce buraya geldi — bir sonraki adımın (`openOptions`/`closeOptions`
ve ardından PIN kilidi) önünü açıyor.

## Owned surface

- `src/Clients/WaiterPwa/wwwroot/js/toast.js` (yeni).
- Sınırlı ek (V1-WTR-010 ailesinin sahipliğinde kalır):
  - src/Clients/WaiterPwa/wwwroot/waiter-app.js — `toast()` ve
    `MAX_VISIBLE_TOASTS` silindi, `import` listesine bir satır eklendi.
  - src/Clients/WaiterPwa/wwwroot/sw.js — `CACHE_NAME` v6'dan v7'ye
    yükseltildi; `ASSETS_TO_CACHE`'e `./js/toast.js` eklendi.

## Out of scope

- PIN kilidi, `openOptions`/`closeOptions`, ekran/sheet modülleri,
  `offline-queue.js`, `push.js` — ayrı görevler.

## Dependencies

- V1-WTR-039

## Acceptance evidence

- `node --check` her iki dosyanın (`js/toast.js`, `waiter-app.js`) da
  sözdizimsel olarak geçerli olduğunu doğruladı.
- `npx playwright test` (`tests/E2E/WaiterPwa`, gerçek Chromium + gerçek
  Postgres + gerçek Host ikili dosyası): **2/2 çalıştırmada 18/18
  temiz** (~43s/çalıştırma).
- `grep -n "function toast(\|const MAX_VISIBLE_TOASTS"` `waiter-app.js`'te
  → 0 eşleşme (taşındı, kopyalanmadı).
- `python tools/consistency-audit/consistency_audit.py` → bu görevin
  değiştirdiği dosyalarda 0 ihlal. Kalan tek ihlal,
  `src/Modules/Inventory/ManualAdjustments/InventoryAdjustmentService.cs:96`,
  bu görevden önce vardı ve sahip olunan yüzeyin dışında.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0
  uyarı.

## Handoff

- None
