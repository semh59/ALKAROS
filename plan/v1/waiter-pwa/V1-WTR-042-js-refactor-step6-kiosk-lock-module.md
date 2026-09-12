# V1-WTR-042 - JS refactor adım 6/?: kiosk-lock.js modülü çıkarma

- Task ID: V1-WTR-042
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

`docs/engineering/garson-refactor-plan.md`'in Bölüm 2.4'ündeki JS
göç stratejisinin devamı: V1-WTR-039'da bilinçli olarak `auth.js`'ten
dışarıda bırakılan PIN kilidi ailesi (`openPinSheet`, `confirmPin`,
`resetIdleTimer`, `lockScreen`, `renderPinDots`, `renderPinPad`,
`submitPin`, `idleTimer`, `IDLE_LOCK_MS`) — ve onunla aynı "Tam ekran"
bölümündeki tam ekran/wake-lock ailesi (`requestWakeLock`,
`releaseWakeLock`, `toggleFullscreen`, `onFullscreenChange`) —
`js/kiosk-lock.js` adlı native ES modülüne taşındı. Bu adım
`toast.js` (V1-WTR-040) ve `options-sheet.js` (V1-WTR-041) hazır
olmadan yapılamazdı; sıralama bilinçliydi.

`openProfileSheet`/`openShiftSummarySheet` bilinçli olarak taşınmadı —
kendi sheet modülleri (planın `sheets/profile.js`'i), bu adımın kapsamı
değil.

## Owned surface

- `src/Clients/WaiterPwa/wwwroot/js/kiosk-lock.js` (yeni).
- Sınırlı ek (V1-WTR-010 ailesinin sahipliğinde kalır):
  - src/Clients/WaiterPwa/wwwroot/waiter-app.js — on bir fonksiyon +
    `idleTimer` + `IDLE_LOCK_MS` silindi, `import` listesine bir satır
    eklendi.
  - src/Clients/WaiterPwa/wwwroot/sw.js — `CACHE_NAME` v8'den v9'a
    yükseltildi; `ASSETS_TO_CACHE`'e `./js/kiosk-lock.js` eklendi.

## Out of scope

- `openProfileSheet`, `openShiftSummarySheet` — yukarıda gerekçelendi.
- Ekran modülleri, kalan yedi sheet modülü, `offline-queue.js`,
  `push.js` — ayrı görevler.

## Dependencies

- V1-WTR-040
- V1-WTR-041

## Acceptance evidence

- `node --check` her iki dosyanın (`js/kiosk-lock.js`, `waiter-app.js`)
  da sözdizimsel olarak geçerli olduğunu doğruladı.
- `npx playwright test` (`tests/E2E/WaiterPwa`, gerçek Chromium + gerçek
  Postgres + gerçek Host ikili dosyası): **3/3 çalıştırmada 18/18
  temiz** (~43s/çalıştırma, güvenlik açısından hassas bir modül olduğu
  için üç kez).
- **Önemli kapsam notu**: bu 18 testin HİÇBİRİ doğrudan PIN kilidini
  veya tam ekranı egzersiz etmiyor (`grep -rn "pin\|lock" specs/`
  kontrol edildi — tek eşleşme alakasız bir yorum satırıydı). 18/18
  yalnızca "bu modülü taşımak paketin test ettiği akışları bozmadı"
  kanıtlıyor, "PIN kilidi taşındıktan sonra hâlâ çalışıyor" kanıtlamıyor.
  Bu gerçek boşluğu kapatmak için PIN kilidini gerçek tarayıcıda
  uçtan uca deneyen GEÇİCİ bir smoke test yazıldı (kur → kilitle → PIN
  ile aç → kaldır), bir kez çalıştırıldı (1/1 geçti — ilk denemede bir
  gerçek test hatası buldu: dört rakam girmek `submitPin()`'i tetiklemez,
  yalnız `data-pin="ok"` tuşu tetikler; test düzeltilip tekrar
  çalıştırıldı), sonra silindi — kalıcı bir spec olarak eklenmedi (bu
  görevin kapsamı bir modül taşımak, yeni test kapsamı eklemek değil).
  **Bu, PIN kilidinin (ve tam ekranın) hâlâ hiçbir kalıcı E2E kapsamı
  olmadığı gerçek, bu görevden önce de var olan bir boşluk** — bir
  sonraki oturuma not: `tests/E2E/WaiterPwa` için gerçek bir PIN kilidi
  senaryosu eklemek ayrı, değerli bir görev olabilir.
- `grep -n "function openPinSheet\|async function confirmPin\|let
  idleTimer\|function resetIdleTimer\|function lockScreen\|function
  renderPinDots\|function renderPinPad\|async function submitPin\|
  function requestWakeLock\|function releaseWakeLock\|function
  toggleFullscreen\|function onFullscreenChange"` `waiter-app.js`'te →
  0 eşleşme (taşındı, kopyalanmadı).
- `python tools/consistency-audit/consistency_audit.py` → bu görevin
  değiştirdiği dosyalarda 0 ihlal. Kalan tek ihlal,
  `src/Modules/Inventory/ManualAdjustments/InventoryAdjustmentService.cs:96`,
  bu görevden önce vardı ve sahip olunan yüzeyin dışında.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0
  uyarı.

## Handoff

- None
