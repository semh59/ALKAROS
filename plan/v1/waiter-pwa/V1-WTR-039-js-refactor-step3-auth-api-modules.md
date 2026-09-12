# V1-WTR-039 - JS refactor adım 3/?: auth.js ve api.js modülleri çıkarma

- Task ID: V1-WTR-039
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

`docs/engineering/garson-refactor-plan.md`'in Bölüm 2.4'ündeki JS
göç stratejisinin **üçüncü adımı**: `js/auth.js` (`applyUser`, `can`,
`trapBackgroundExcept`/`releaseTrap`, `showLogin`) ve `js/api.js`
(`apiUrl`, `api()`) çıkarıldı. Tek yönlü bağımlılık: `api.js` → `auth.js`
(401 kesicisi `showLogin()`'i çağırıyor); `auth.js`'in `api.js`'e
bağımlılığı yok — dairesel import hiç oluşmadı.

Planın kendi listesinden bilinçli bir sapma: plan `auth.js`'e ayrıca
PIN kilidini de sayıyordu (`openPinSheet`, `resetIdleTimer`,
`lockScreen`, `renderPinDots`/`Pad`) ve `submitLogin`/`hasValidSession`'ı
da örtük olarak aynı aile sayılabilirdi. Kod incelemesi bunların ikisinin
de bu adımın beş temel primitifinden daha büyük, render'a daha sıkı
bağımlı, ayrı bir sorumluluk olduğunu gösterdi — ayrı bir sonraki adıma
bırakıldı, zorla aynı dosyaya sıkıştırılmadı (Bölüm 1.3'ün "yanlış
soyutlama riski" ilkesiyle aynı gerekçe).

## Owned surface

- `src/Clients/WaiterPwa/wwwroot/js/auth.js` (yeni).
- `src/Clients/WaiterPwa/wwwroot/js/api.js` (yeni).
- Sınırlı ek (V1-WTR-010 ailesinin sahipliğinde kalır):
  - src/Clients/WaiterPwa/wwwroot/waiter-app.js — beş fonksiyon +
    `trapStack` silindi, en üstteki `import` listesine iki modülden
    isimler eklendi; artık kullanılmayan `describeHttpFailure` import'u
    kaldırıldı (yalnız `api.js` kendi içinde kullanıyor artık).
  - src/Clients/WaiterPwa/wwwroot/sw.js — `CACHE_NAME` v5'ten v6'ya
    yükseltildi; `ASSETS_TO_CACHE`'e `./js/auth.js` ve `./js/api.js`
    eklendi.

## Out of scope

- Planın kalan adımları — PIN kilidi kendi başına bir sonraki adım
  olarak bırakıldı (yukarıda gerekçelendirildi), ekran/sheet modülleri,
  `offline-queue.js`, `push.js`.
- `submitLogin`, `hasValidSession`, `init()` — hâlâ `waiter-app.js`'te,
  hem `auth.js`'in hem `api.js`'in primitiflerini kullanıyorlar ama
  kendileri giriş AKIŞI/orkestrasyonu, bu adımın "primitif" kapsamının
  dışında.

## Dependencies

- V1-WTR-038

## Acceptance evidence

- `node --check` üç dosyanın da (`js/auth.js`, `js/api.js`,
  `waiter-app.js`) sözdizimsel olarak geçerli olduğunu doğruladı.
- `npx playwright test` (`tests/E2E/WaiterPwa`, gerçek Chromium + gerçek
  Postgres + gerçek Host ikili dosyası): **2/2 çalıştırmada 18/18
  temiz** (~43s/çalıştırma) — özellikle giriş akışı testi
  (`01-login.spec.js`) `showLogin`'in idempotency düzeltmesinin
  (V1-RMD-178) taşındıktan sonra da doğru çalıştığını kanıtlıyor.
- `grep -n "function applyUser\|function can(\|function
  trapBackgroundExcept\|function releaseTrap\|function showLogin\|
  function apiUrl\|async function api("` `waiter-app.js`'te → 0
  eşleşme (taşındı, kopyalanmadı).
- `python tools/consistency-audit/consistency_audit.py` → bu görevin
  değiştirdiği dosyalarda 0 ihlal. Kalan tek ihlal,
  `src/Modules/Inventory/ManualAdjustments/InventoryAdjustmentService.cs:96`,
  bu görevden önce vardı ve sahip olunan yüzeyin dışında.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0
  uyarı.

## Handoff

- None
