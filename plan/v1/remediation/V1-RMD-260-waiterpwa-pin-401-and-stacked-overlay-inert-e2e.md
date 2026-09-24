# V1-RMD-260 - WaiterPwa: yanlış PIN/şifre oturum bitişi sayılmaz, üst üste açılan katman canlı kalır; kalıcı offline-kuyruk ve kiosk-kilidi E2E spec'leri

- Task ID: V1-RMD-260
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: remediation
- Surface state: Existing

## Source basis

- PO:2026-09-24

## Goal

`docs/engineering/e2e-playwright-master-test-plan.md`'nin Faz 2 önerisini
uygulamak: WaiterPwa'nın hiçbir tarayıcı kapsamı olmayan iki alanı için —
çevrimdışı sipariş kuyruğu (sipariş kaybına yol açabilecek tek yer) ve PIN
kiosk kilidi (güvenlik hatası geçmişi olan yer) — kalıcı Playwright
spec'leri yazmak. Spec'ler ilk dürüst koşuda **iki gerçek üretim hatası**
buldu; ikisi de düzeltildi.

**Hata 1 — her yanlış PIN garsonu tam girişe atıyordu.** Sunucu, yanlış PIN
için (`/auth/unlock`, kod `INVALID_PIN`) ve PIN kurarken yanlış mevcut şifre
için (`/auth/pin`, kod `INVALID_CREDENTIALS`) 401 döndürür. `api.js` her 401'i
"oturum bitti" sayıp `showLogin()` çağırıyordu; `showLogin()` da kilit
katmanını gizler. Sonuç: kilit ekranında tek bir yanlış PIN, kilidi kapatıp
kullanıcı adı/şifre girişini açıyordu — "PIN hatalı, tekrar deneyin" akışı ve
5 denemelik sunucu kilitlemesi pratikte hiç görülemiyordu, PIN kurarken şifre
yanlış girilince de garson girişe atılıyordu.

**Hata 2 — açık bir sayfa varken gelen kilit/giriş katmanı ölüydü.**
`openOptions` arka planın tamamını (kilit ve giriş katmanları dahil) `inert`
yapar. Sonradan `lockScreen()` veya `showLogin()` çağrılınca
`trapBackgroundExcept` "zaten inert olanlara dokunma" kuralı yüzünden kendi
aktif katmanını hiç geri açmıyordu: katman görünüyor ama PIN tuşları ve giriş
alanları tıklanamıyor/odaklanamıyordu. Boşta kilit, bir sayfa açıkken devreye
girerse garson kilidi açamıyor; oturum bitişinde de giriş yapamıyordu.

## Owned surface

- `plan/v1/remediation/V1-RMD-260-waiterpwa-pin-401-and-stacked-overlay-inert-e2e.md`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/WaiterPwa/wwwroot/js/api.js
  (V1-WTR-039 sahipliğinde kalır — yalnız 401 işleme: `INVALID_PIN` ve
  `INVALID_CREDENTIALS` kodları artık `showLogin()` tetiklemiyor)
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/WaiterPwa/wwwroot/js/auth.js
  (V1-WTR-038 sahipliğinde kalır — yalnız `trapBackgroundExcept`/`releaseTrap`:
  her katman kendi aktif öğelerini de geri açar ve pop'ta geri verir)
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/E2E/WaiterPwa/specs/06-offline-queue.spec.js
  (V1-WTR-026 sahipliğindeki WaiterPwa E2E paketine eklenen yeni dosya)
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/E2E/WaiterPwa/specs/07-kiosk-lock.spec.js
  (aynı paket, yeni dosya)
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/E2E/WaiterPwa/lib/seed.js
  (V1-WTR-026 sahipliğinde kalır — yalnız 8 adet dokunulmamış `OFF-n` masası
  eklendi)

## In scope

1. `api.js`: `INVALID_PIN` / `INVALID_CREDENTIALS` kodlu 401 yanıtları bir
   kimlik-bilgisi doğrulamasının cevabıdır, oturum bitişi değildir; yalnız
   diğer 401'ler `showLogin()` çağırır.
2. `auth.js`: tuzak yığınındaki her kayıt, o çağrının yeni `inert` yaptığı
   öğelere ek olarak yeni geri açtığı aktif öğeleri de tutar; `releaseTrap`
   ikisini de geri verir (alttaki tuzak durumu korunur).
3. Spec 06 (10 test): çevrimdışı gönderim kuyruğa girer ve `online` olayıyla
   anında gider; flush sırasında 4xx tur sessizce silinmez, hatalı listesine
   taşınır (Sil çalışır); 429 ve 503'te tur kuyrukta KALIR ve düzelince gider;
   yenilemede kuyruk ve rozet korunur; bozuk localStorage uygulamayı
   çökertmez; `visibilitychange` bekleyen turu gönderir; çevrimiçi 503/429/4xx
   canlı gönderim dalları; `sortQueueByPriority` (kurs sırası, en eski, 2 dk
   yaşlanma).
4. Spec 07 (11 test): PIN kurma/kaldırma ve doğrulamalar, tuş takımı (yanlış
   PIN, 12 hane sınırı, silme), Escape ve Tab ile kilitten kaçış yok, sayfa
   açıkken kilit ve oturum bitişi (yukarıdaki Hata 2), negatif kontrol, 3
   dakikalık boşta kilit (`page.clock`), tam ekran çıkışında kilit, 423
   devri, 409 bayat bayrak.

## Out of scope

- Sunucu tarafı PIN/kilitleme mantığı — doğru, dokunulmadı.
- Kilidin sayfa yenilemesinde kalıcı olmaması (kilit yalnız istemci
  belleğinde tutulur; yenileme kilitsiz açar) — bilinen ve belgelenmiş
  tasarım sınırı, burada değiştirilmedi; ayrı bir ürün kararı.
- Push bildirimleri, ekran okuyucu ve gerçek cihaz doğrulaması.

## Dependencies

- V1-WTR-026
- V1-WTR-053
- V1-RMD-151
- V1-RMD-178
- V1-RMD-180

## Acceptance evidence

- WaiterPwa suite'i (`tests/E2E/WaiterPwa`, gerçek Host, gerçek Chromium,
  **UTF8 Postgres 18**): **39/39 geçti** (18 önceki + 21 yeni).
- **Mutasyon kontrolü.** Offline kuyruk: flush'ta 4xx'in hatalıya taşınması
  bozuldu, 429 dalı kaldırıldı, yaşlanma sabiti büyütüldü, `visibilitychange`
  ve `online` flush çağrıları kaldırıldı — her biri ilgili testi kırdı
  (ilk denemede `visibilitychange` testi geçmişti çünkü 15 sn'lik geri-deneme
  zamanlayıcısı işi kendiliğinden bitiriyordu; bekleme süreleri zamanlayıcının
  altına çekilerek ayırt edici hâle getirildi). Kiosk: `api.js` ve `auth.js`
  düzeltmeleri geçici olarak tersine uygulandı → 5 test kırıldı (1, 2, 4, 5,
  6); yeniden uygulanınca 11/11. Her mutasyondan sonra `git diff -- src/`
  beklenen düzeltmeler dışında boştu.
- Cashier suite'i (23/23) aynı UTF8 ortamda yeniden koşturuldu.
- **Ortam bulgusu (ürün hatası DEĞİL):** bu makinede `localhost:55432`'yi
  Docker'daki `alkaros-test-pg` değil, yerel bir Windows PostgreSQL 18.6
  (`SQL_ASCII` kodlaması) yanıtlıyor. O sunucuda Türkçe karakterli JSON
  kaçışları (`ğ`) `jsonb`'ye girerken `22P05` veriyor; bu yüzden mevcut
  spec 03 ("masa devri", 503) ve spec 04 (performans) o ortamda kırılıyordu.
  UTF8 bir Postgres'te ikisi de geçiyor. E2E'ler bu yüzden ayrı bir UTF8
  konteynerde (port 56433) koşturuldu.
- Dürüstçe belirtilen sınırlar: test suite'i CI'da koşmuyor; kiosk kilidi
  yenilemede kalıcı değil (Out of scope); gerçek cihaz ve ekran okuyucu
  doğrulaması yok.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → temiz.

## Handoff

- None
