# V1-WTR-051 - JS refactor adım 15/?: push.js modülü çıkarma

- Task ID: V1-WTR-051
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

`docs/engineering/garson-refactor-plan.md`'in Bölüm 2.4'ündeki JS
göç stratejisinin devamı: Web Push ailesi (`pushSupported`,
`iosNeedsInstall`, `currentPushSubscription`, `refreshPushState`,
`enablePush`, `unsubscribePush`, `disablePush`, `base64UrlToBytes`)
`js/push.js`'e taşındı.

Planın kendi metni bunu `offline-queue.js` ile birlikte "en riskli iki
modül" sayıyordu ("burada bir hata sessizce sipariş kaybına yol
açabilir"). Kod incelemesi bu riskin tamamen `offline-queue.js`'e ait
olduğunu gösterdi — `push.js` tamamen bağımsız (yalnız `state`, `toast`,
`api`/`apiUrl` ve tarayıcı API'lerine bağımlı), hiçbir sipariş/taslak
mantığına dokunmuyor. Bu adım, planın genel risk uyarısını doğru
okuyup gerçek bağımlılık grafiğine göre değerlendirmenin bir örneği.

## Owned surface

- `src/Clients/WaiterPwa/wwwroot/js/push.js` (yeni).
- Sınırlı ek (V1-WTR-010 ailesinin sahipliğinde kalır):
  - src/Clients/WaiterPwa/wwwroot/waiter-app.js — sekiz fonksiyon
    silindi, `import` listesine bir satır eklendi.
  - src/Clients/WaiterPwa/wwwroot/sw.js — `CACHE_NAME` v17'den v18'e
    yükseltildi; `ASSETS_TO_CACHE`'e `./js/push.js` eklendi.

## Out of scope

- `sheets/profile.js` (artık bu modülün hazır olmasıyla önü açıldı,
  ayrı bir görev) ve `offline-queue.js` — ayrı görevler.

## Dependencies

- V1-WTR-040

## Acceptance evidence

- `node --check` her iki dosyanın (`js/push.js`, `waiter-app.js`) da
  sözdizimsel olarak geçerli olduğunu doğruladı. Her import edilen
  ismin gerçekten kullanıldığı tek tek `grep`'le kontrol edildi
  (`pushSupported` ilk taslakta import edilmişti ama dışarıdan hiç
  çağrılmadığı görülüp import listesinden çıkarıldı — kendi taşınan
  fonksiyonların İÇİNDE kullanılması yeterli değil, DIŞARIDAN da
  çağrılması gerekiyor).
- `npx playwright test` (`tests/E2E/WaiterPwa`, gerçek Chromium + gerçek
  Postgres + gerçek Host ikili dosyası): **2/2 çalıştırmada 18/18
  temiz** (~43s/çalıştırma).
- **Kapsam notu**: bu 18 testin hiçbiri push bildirimini doğrudan test
  etmiyor (`grep -rln "push\|Push" specs/` → 0 eşleşme) — V1-WTR-042'nin
  (kiosk-lock) PIN kilidi için bulduğu aynı gerçek boşluk burada da
  var, bu görevden önce de vardı. 18/18 yalnızca "bu taşıma test edilen
  akışları bozmadı" kanıtlıyor, push'un kendisinin hâlâ çalıştığını
  kanıtlamıyor.
- `grep -n "^  function pushSupported\|^  function iosNeedsInstall\|^
  async function currentPushSubscription\|^  async function
  refreshPushState\|^  async function enablePush\|^  async function
  unsubscribePush\|^  async function disablePush\|^  function
  base64UrlToBytes"` `waiter-app.js`'te → 0 eşleşme (taşındı,
  kopyalanmadı).
- `python tools/consistency-audit/consistency_audit.py` → bu görevin
  değiştirdiği dosyalarda 0 ihlal. Kalan tek ihlal,
  `src/Modules/Inventory/ManualAdjustments/InventoryAdjustmentService.cs:96`,
  bu görevden önce vardı ve sahip olunan yüzeyin dışında.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0
  uyarı.

## Handoff

- None
