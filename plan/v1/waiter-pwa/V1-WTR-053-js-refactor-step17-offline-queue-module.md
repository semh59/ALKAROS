# V1-WTR-053 - JS refactor adım 17/17: offline-queue.js — planın son ve en riskli modülü

- Task ID: V1-WTR-053
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

`docs/engineering/garson-refactor-plan.md`'in Bölüm 2.4'ündeki JS
göç stratejisinin **son adımı**: `draftToPayload`, `postOrder`,
`sendDraft`, `removeSentDraftLines`, `persistQueue`, `queueOrder`,
`scheduleQueueRetry` (+ kendi sabitleri/değişkenleri), `flushQueue`
`js/offline-queue.js`'e taşındı. Bu, planın kendi metninin "burada bir
hata sessizce sipariş kaybına yol açabilir" diye açıkça uyardığı,
bilinçli olarak en son bırakılan modül.

Bu adım tamamlanınca planın Bölüm 2.4'ündeki HER modül gerçekleşti —
`waiter-app.js` artık yalnız giriş noktası: `import` grafiği,
`bindEvents()`, ve sayfa yüklenince çalışan `init()` akışı.

Bu adım ayrıca üç önceki adımda bilinçli olarak ertelenmiş parçayı da
tamamladı (`persistQueue`/`renderBill` artık modül olarak var):

- `confirmPartySize` → `js/sheets/party-size.js`'e taşındı
  (V1-WTR-044'ün ertelediği).
- `fireCourse` → `js/sheets/bill.js`'e taşındı.
- `dismissFailedOrder`/`clearAllFailedOrders` → `js/sheets/
  failed-orders.js`'e taşındı (V1-WTR-050'nin ertelediği).

## Owned surface

- `src/Clients/WaiterPwa/wwwroot/js/offline-queue.js` (yeni).
- Sınırlı ek (V1-WTR-010 ailesinin sahipliğinde kalır):
  - src/Clients/WaiterPwa/wwwroot/js/state.js — `renderRibbon` ve
    `OFFLINE_DISABLED_REASONS` eklendi (V1-WTR-038'in sahipliğinde
    kalır).
  - src/Clients/WaiterPwa/wwwroot/js/sheets/party-size.js —
    `confirmPartySize` eklendi (V1-WTR-044'ün sahipliğinde kalır).
  - src/Clients/WaiterPwa/wwwroot/js/sheets/bill.js — `fireCourse`
    eklendi (V1-WTR-045'in sahipliğinde kalır).
  - src/Clients/WaiterPwa/wwwroot/js/sheets/failed-orders.js —
    `dismissFailedOrder`/`clearAllFailedOrders` eklendi (V1-WTR-050'nin
    sahipliğinde kalır).
  - src/Clients/WaiterPwa/wwwroot/waiter-app.js — on iki fonksiyon +
    bir sözlük silindi, `import` listesi güncellendi; tüm dosyanın
    import listesi programatik bir script'le (her isim en az bir kez
    dışarıdan gerçekten kullanılıyor mu) yeniden tarandı, iki fazladan
    kullanılmayan import bulundu (`randomUUID`, `loadOrder`) ve
    kaldırıldı.
  - src/Clients/WaiterPwa/wwwroot/sw.js — `CACHE_NAME` v19'dan v20'ye
    yükseltildi; `ASSETS_TO_CACHE`'e `./js/offline-queue.js` eklendi.

## Out of scope

- `waiter-app.js`'in kendisinin daha da küçültülmesi — artık yalnız
  giriş noktası (import grafiği + `bindEvents()` + `init()`), planın
  kendi hedef mimarisinin tam olarak öngördüğü son hâli.
- TypeScript'e geçiş, birim testi eklenmesi, `OrderReadStore`'un daha
  fazla bölünmesi — planın Bölüm 4'ünün kendi bilinçli erteledikleri.

## Dependencies

- V1-WTR-045
- V1-WTR-046
- V1-WTR-050

## Acceptance evidence

Bu görevde bulunan ve düzeltilen gerçek bir hata (kanıtın parçası):
`persistQueue()`'yu taşırken orijinal kodun sonunda `renderRibbon()`
çağrısı vardı — bu çağrıyı ilk taslakta atladım (yanlışlıkla "her
çağıran zaten kendi render'ını tetikliyor" diye düşünüp). Bu, ribbon'un
"N bekleyen/hatalı" rozetinin artık anında güncellenmemesi anlamına
gelirdi — E2E paketinin HİÇBİR testi bu rozetin metnini kontrol etmiyor,
bu yüzden 18/18 bunu YAKALAMAZDI. Kendi bağımlılık analizimde fark edip
kodu commit etmeden düzelttim (`renderRibbon`'ı `js/state.js`'e taşıyıp
`offline-queue.js`'in oradan import edip çağırmasını sağladım) — bu
"E2E yeşil ≠ her davranış doğrulanmış" dersinin tam bir örneği.

- `node --check` altı dosyanın da (`js/offline-queue.js`, `js/state.js`,
  `js/sheets/party-size.js`, `js/sheets/bill.js`,
  `js/sheets/failed-orders.js`, `waiter-app.js`) sözdizimsel olarak
  geçerli olduğunu doğruladı.
- Programatik import-kullanım taraması: `waiter-app.js`'in TÜM 73 import
  isminin her biri gövdede en az bir kez gerçekten kullanılıyor (0
  kullanılmayan). Ayrıca ters yönde bir tarama (çağrılan ama tanımlı/
  import edilmemiş bir isim var mı) yapıldı — iki "şüpheli" eşleşme
  (`draftQuantityOf`, `resolvePending`) kontrol edilip yalnız yorum
  satırlarında olduğu, gerçek kod olmadığı doğrulandı.
- `npx playwright test` (`tests/E2E/WaiterPwa`, gerçek Chromium + gerçek
  Postgres + gerçek Host ikili dosyası): **5/5 çalıştırmada 18/18
  temiz** (~44s/çalıştırma, planın kendi risk uyarısı nedeniyle beş
  kez) — `05-load-and-timing.spec.js`'in kendi yük testi (6 eşzamanlı
  garson, gerçek round gönderiyor) bu modülün gerçek kapsamını doğrudan
  doğruluyor.
- **Gerçek çevrimdışı-kuyruk davranışı ayrıca doğrudan test edildi**:
  hiçbir kalıcı E2E spec'i tarayıcıyı gerçekten çevrimdışına almıyor
  (`grep -rln "offline\|setOffline" specs/` → 0 eşleşme, V1-WTR-042/051'in
  PIN kilidi/push için bulduğu aynı boşluk sınıfı). Bunu gerçek kanıtla
  kapatmak için GEÇİCİ bir smoke test yazıldı (Playwright'ın
  `context.setOffline(true)`'ı ile gerçekten çevrimdışına al → ürün ekle
  → gönder → ribbon "1 bekleyen" gösteriyor mu doğrula → çevrimiçine al
  → kuyruğun kendiliğinden boşaldığını doğrula), 1/1 geçti (ilk denemede),
  sonra silindi — kalıcı bir spec olarak eklenmedi (bu görevin kapsamı
  bir modül taşımak, yeni test kapsamı eklemek değil, ama bu modülün
  gerçek davranışını kanıtlamadan "taşıdım, test yeşil" demek yeterli
  değildi).
- `grep -n "^  function draftToPayload\|^  async function postOrder\|^
  async function sendDraft\|^  function removeSentDraftLines\|^
  function persistQueue\|^  function queueOrder\|^  function
  scheduleQueueRetry\|^  async function flushQueue\|^  function
  confirmPartySize\|^  async function fireCourse\|^  function
  dismissFailedOrder\|^  function clearAllFailedOrders\|^  function
  renderRibbon\|^  const OFFLINE_DISABLED_REASONS"` `waiter-app.js`'te
  → 0 eşleşme (taşındı, kopyalanmadı).
- `python tools/consistency-audit/consistency_audit.py` → bu görevin
  değiştirdiği dosyalarda 0 ihlal. Kalan tek ihlal,
  `src/Modules/Inventory/ManualAdjustments/InventoryAdjustmentService.cs:96`,
  bu görevden önce vardı ve sahip olunan yüzeyin dışında.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0
  uyarı.
- `waiter-app.js`: 3295 satırdan 776 satıra düştü (%76 azalma) —
  yaklaşık 2519 satır, 22 native ES modülüne bölündü, 17 görev ID'si
  (V1-WTR-037..053) boyunca, her biri kendi build+E2E kanıtıyla.

## Handoff

- None
