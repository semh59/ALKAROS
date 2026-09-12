# V1-WTR-038 - JS refactor adım 2/?: state.js modülü çıkarma

- Task ID: V1-WTR-038
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

`docs/engineering/garson-refactor-plan.md`'in Bölüm 2.4'ündeki JS
göç stratejisinin **ikinci adımı**: `state` (paylaşılan uygulama durumu)
ve `el` (DOM eleman önbelleği) — dosyanın en kritik iki paylaşılan
closure değişkeni — `js/state.js` adlı native ES modülüne taşındı.
Davranış değişmedi: ikisi de hâlâ sıradan mutable JS nesneleri, bir
`import` binding'i sabit olsa da nesnenin kendisi değil — `state.draft =
[]` herhangi bir modülden hâlâ TEK paylaşılan örneği değiştiriyor,
eski closure'ın yaptığı gibi.

Planın kendi listesinden bilinçli bir sapma: `el`, planın state.js
tanımında açıkça sayılmıyor (yalnız "aktif masa, taslak, kullanıcı,
izin seti" örnekleri veriyor), ama `state` ile birebir aynı riski
taşıyor (dosyanın her yerinden dokunulan paylaşılan mutable closure) ve
aynı çözümden (`import { el } from ...`) yararlanıyor — bu yüzden aynı
adımda birlikte taşındı. `IDLE_LOCK_MS` ve Türkçe sözlükler
(`TABLE_STATUS`, `KITCHEN_STATE`, `VOID_REASONS`, `COMP_REASONS`)
bilinçli olarak taşınmadı — bunlar durum değil, sabit veri; planın
"state.js" tanımının kapsamına girmiyorlar ve nereye ait olduklarına
(muhtemelen ilerideki bir `dictionaries.js` veya kalacakları yer) şimdi
karar vermek erken.

`state.draftsByTable`'ın ilk değerini okuyan `loadDraftsByTable()` da
bu adımda `state.js`'e taşındı (yalnız `localStorage` okuyan saf bir
fonksiyon, `state`/`el`'e bağımlı değil) — yazma tarafı
(`persistDraftsByTable`) planın kendi 5. adımında (`offline-queue.js`)
taşınacağı için `waiter-app.js`'te kalıyor.

## Owned surface

- `src/Clients/WaiterPwa/wwwroot/js/state.js` (yeni) — `state`, `el`,
  `state.draftsByTable`'ın ilk değeri için özel `loadDraftsByTable()`.
- Sınırlı ek (V1-WTR-010 ailesinin sahipliğinde kalır):
  - src/Clients/WaiterPwa/wwwroot/waiter-app.js — `state`/`el`
    tanımları ve `loadDraftsByTable()` silindi, en üstteki `import`
    listesine `state.js`'ten bir satır eklendi; kullanılmayan
    `deviceTerminalId` import'u kaldırıldı (artık yalnız `state.js`
    kendi içinde kullanıyor).
  - src/Clients/WaiterPwa/wwwroot/sw.js — `CACHE_NAME` v4'ten v5'e
    yükseltildi; `ASSETS_TO_CACHE`'e `./js/state.js` eklendi.

## Out of scope

- Planın kalan adımları (`api.js`, `auth.js`, ekran/sheet modülleri,
  `offline-queue.js`, `push.js`) — ayrı görevler.
- Türkçe sözlükler ve `IDLE_LOCK_MS` — yukarıda gerekçelendirildiği
  gibi bu adıma dahil değil.

## Dependencies

- V1-WTR-037

## Acceptance evidence

- `node --check src/Clients/WaiterPwa/wwwroot/js/state.js` ve
  `waiter-app.js` → ikisi de sözdizimsel olarak geçerli.
- `npx playwright test` (`tests/E2E/WaiterPwa`, gerçek Chromium + gerçek
  Postgres + gerçek Host ikili dosyası): **2/2 çalıştırmada 18/18
  temiz** (~43s/çalıştırma).
- `grep -n "function loadDraftsByTable\|const state = {\|const el =
  {}" waiter-app.js` → 0 eşleşme (taşındı, kopyalanmadı).
- `python tools/consistency-audit/consistency_audit.py` → bu görevin
  değiştirdiği dosyalarda 0 ihlal. Kalan tek ihlal,
  `src/Modules/Inventory/ManualAdjustments/InventoryAdjustmentService.cs:96`,
  bu görevden önce vardı ve sahip olunan yüzeyin dışında.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0
  uyarı.

## Handoff

- None
