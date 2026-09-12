# V1-WTR-026 - WaiterPwa gerçek tarayıcı denetimi (Playwright E2E)

- Task ID: V1-WTR-026
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

Semih'in talimatıyla ("Ben sadece kursu değil tüm garson modülünü denetle",
2026-09-12, seçilen kapsam: "Otomatik Playwright test paketi"): WaiterPwa'yı
gerçek Chrome'da, gerçek Postgres + gerçek Host'a karşı uçtan uca denetleyen
kalıcı bir Playwright test paketi kuruldu — mock/stub yok, bu görevin asıl
amacı da buydu: hiçbir mevcut xUnit/HTTP testi gerçek `waiter-app.js`
dosyasını hiç çalıştırmıyor, yalnızca sunucu tarafını.

**Bu denetim, hiçbir mevcut testin yakalayamayacağı iki gerçek ve ciddi
üretim hatası buldu, ikisi de bu görevde düzeltildi:**

1. **Her taze girişten sonra tüm uygulama tıklanamaz hale geliyordu.**
   `api()`'nin genel 401 yakalayıcısı (`showLogin()`) VE `init()`'in kendi
   açık `showLogin()` çağrısı, sayfa ilk açıldığında henüz oturum yokken
   atılan TEK bir 401 için İKİ KEZ çalışıyordu — her ikisi de
   `trapBackgroundExcept()` ile arka planı (`#screens` dahil) "inert"
   yapıyor. `submitLogin()` başarılı girişten sonra `releaseTrap()`'ı
   yalnız BİR KEZ çağırıyor (LIFO yığından tek katman kaldırıyor), ikinci
   katman hiç kaldırılmıyor — `#screens` kalıcı olarak `inert` kalıyor,
   giriş ekranı kapanmasına rağmen hiçbir masaya, hiçbir butona
   tıklanamıyor, sayfa tam yenilemeden düzelmiyor. `showLogin()` artık
   idempotent: overlay zaten görünürse ikinci kez tuzak kurmuyor.
2. **Her "Gönder" (mutfağa gönder) tıklaması sessizce başarısız oluyordu.**
   `postOrder()`'ın submit-draft isteğinde tanımsız bir `headers` değişkenine
   çıplak referans vardı (V1-RMD-163'ün fazlalık X-Idempotency-Key
   header'ını kaldırırken değeri silip bu referansı unuttuğu bir kalıntı).
   Bu, `fetch()` hiç çağrılmadan `ReferenceError: headers is not defined`
   fırlatıyordu — table-draft her zaman başarılı oluyordu ama submit-draft
   HİÇ denenmiyordu, hata hiçbir yerde gösterilmiyordu
   (`sendDraft()`'ın try/finally'sinde catch yok). Sipariş hiçbir zaman
   mutfağa ulaşmıyordu. Tanımsız değişken referansı kaldırıldı.

Her iki hata da gerçek tarayıcıda gerçek bir tıklama zincirinde ortaya
çıktı; hiçbiri statik analiz, unit test veya HTTP entegrasyon testiyle
yakalanamazdı çünkü hiçbiri bu JS dosyasını gerçekten yürütmüyor.

## Owned surface

- `tests/E2E/WaiterPwa/**` (yeni) — kalıcı Playwright E2E paketi:
  `global-setup.js` (gerçek migrasyon + seed + gerçek `ALKAROS.Host.dll
  serve` süreci başlatma/durdurma), `lib/migrate.js`, `lib/seed.js`,
  `lib/passwordHash.js`, `lib/testHelpers.js`, `specs/01..04-*.spec.js`,
  `playwright.config.js`, `package.json`, `package-lock.json`,
  `.gitignore`, `README.md` (kurulum/çalıştırma talimatları + karşılaşılan
  Node 24/Playwright 1.48 uyumsuzluğu notu).
- Sınırlı ek:
  - src/Clients/WaiterPwa/wwwroot/waiter-app.js (WaiterPwa sahipliğinde)
    — `showLogin()` idempotent yapıldı, `postOrder()`'daki tanımsız
    `headers` referansı kaldırıldı.

## Out of scope

- Yük/eşzamanlılık testi — bu paket tek tarayıcı, tek istek; "hız ölçümü"
  yalnızca gerçek bir tarayıcıda tipik yanıt sürelerinin gevşek bir
  regresyon-tuzağı (trip-wire), sıkı bir SLA iddiası değil.
- Kapsanmayan akışlar: kasaya gönder (`send-to-cashier`), bahşiş (zaten
  istemci arayüzü yok — V1-WTR-020), Web Push, offline kuyruk, PIN kilidi.
  Bunlar ayrı, daha derin bir tarayıcı denetimi isterse sonraki görev.
- CI entegrasyonu (bu paketi otomatik pipeline'a bağlamak) — yalnızca
  manuel/yerel çalıştırma için kuruldu, README bunu netleştiriyor.

## Dependencies

- V1-WTR-025

## Acceptance evidence

- `ALKAROS_TEST_PG_PORT=55432 ALKAROS_TEST_PG_PASSWORD=postgres npx
  playwright test` (tests/E2E/WaiterPwa, temiz `npm install` +
  `npx playwright install chromium` sonrası, sıfırdan çalıştırıldı):
  **16/16 test yeşil** — gerçek giriş (yanlış/doğru şifre), gerçek masa
  açma, koltuk+kurs+modifier ile ürün ekleme, kurssuz hızlı ekleme, gerçek
  mutfağa gönderim (gerçek kitchen ticket), kurs bekletme/ateşleme
  (V1-WTR-025'in kendi özelliği, gerçek tarayıcıda uçtan uca doğrulandı),
  gönderilmiş kalemi ikram etme, gönderilmiş kalem için iptal isteği, masa
  taşıma, garson devri + devir notu, yardım çağırma, vardiya özeti okuma.
- Her iki üretim hatası da önce gerçek bir başarısızlıkla (30 saniyelik
  timeout / fırlatılan `ReferenceError`) doğrulandı, düzeltmeden SONRA
  aynı senaryo yeşile döndü — körü körüne "düzeltildi" denmedi.
- `dotnet build ALKAROS.slnx -c Debug` (tüm çözüm, bu görev yalnızca JS
  değiştirdi ama .NET tarafı regresyona karşı kontrol edildi) → 0 uyarı,
  0 hata.
- `python tools/consistency-audit/consistency_audit.py` → bu görevin
  değiştirdiği dosyalarda 0 ihlal. Kalan tek ihlal,
  `src/Modules/Inventory/ManualAdjustments/InventoryAdjustmentService.cs:96`,
  bu görevden önce vardı ve sahip olunan yüzeyin dışında.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.

## Handoff

- None
