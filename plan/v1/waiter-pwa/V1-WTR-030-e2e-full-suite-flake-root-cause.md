# V1-WTR-030 - E2E paketinin tam koşumda görülen flake'inin kök nedeni

- Task ID: V1-WTR-030
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

Semih'in talimatıyla ("Düzelt", 2026-09-12, E2E paketinin tam koşumda
(yalnız `05-load-and-timing.spec.js` değil) bazen görülen tek testlik
başarısızlığa işaret ederek) — V1-WTR-028'in kendi "Out of scope"
notunda "makine/CPU rekabeti" olarak (doğrulanmadan) bırakılmış bu
flake'in **gerçek kök nedeni** bulundu ve kapatıldı.

**İlk hipotez YANLIŞ çıktı:** `02-ordering.spec.js` ve
`03-waiter-actions.spec.js`'in her ikisi de `browser.newContext()` ile
paylaşılan bir context açıp dosyanın sonunda **hiç kapatmıyordu** —
gerçek bir kaynak sızıntısıydı, düzeltildi (aşağıda), ama tam paketi 5
kez art arda çalıştırıp doğrulayınca flake **hiç değişmeden devam etti**
— yani "CPU/kaynak rekabeti" hipotezi (V1-WTR-028'in kendi notu) hiç
doğrulanmamış, yanlış bir tahmindi.

**Gerçek kök neden (Host'un kendi loglarında bulundu, tahmin
edilmedi):** `E2E_HOST_LOG=1` ile tam paket çalıştırılıp başarısızlığın
tam anındaki gerçek istekler incelendiğinde, `POST /api/v1/auth/login`
isteklerinden ikisinin **gerçek bir 429 (Too Many Requests)** döndüğü
görüldü. `DualScreenApplication.cs`'in `"login"` rate-limit politikası
yalnız istemci IP'sine göre bölümleniyor (`ClientPartition`), dakikada
10 istek. Bir Playwright koşumundaki HER test aynı IP'den (127.0.0.1)
bağlandığı için, tüm ~45-60 saniyelik koşum **TEK bir kovaya** düşüyor —
spec 01-04'ün kendi girişleri, `05`'in 6 eşzamanlı girişinden ÖNCE bu
kovanın büyük kısmını zaten harcamış oluyor, ve 6 yeni eşzamanlı giriş
bunlardan 2'sini limitin üzerine itip gerçek bir 429'a düşürüyor —
`login()` yardımcı fonksiyonu bunu `#tablesGrid` görünür olmasını
bekleyen bir zaman aşımı olarak görüyor (429'un kendi hata mesajı hiç
okunmuyor, yalnız "element bulunamadı" raporlanıyor, bu yüzden gerçek
sebep gizli kalmıştı).

Bu, gerçek üretimde **doğru ve istenen** bir davranış (kaba kuvvet
girişimine karşı) — E2E paketi yalnız tek bir IP'den, gerçek bir
istemcinin asla üretmeyeceği kadar yoğun trafik ürettiği için bu
korumaya takılıyordu. Çözüm bu yüzden varsayılan değeri gevşetmek değil,
**yalnız test ortamına özel** bir geçersiz kılma mekanizması eklemekti.

## Owned surface

- Sınırlı ek:
  - src/Host/DualScreen/DualScreenApplication.cs (V1-RMD-1xx/DualScreen
    sahipliğinde) — `"login"` rate-limit politikasının izin sayısı artık
    `LoginRateLimitPermits()` üzerinden okunuyor: `ALKAROS_LOGIN_RATE_LIMIT_PERMITS`
    ortam değişkeni ayarlıysa onu kullanıyor, değilse mevcut `10`
    varsayılanına düşüyor (hiçbir gerçek dağıtımda davranış değişmedi).
  - tests/E2E/WaiterPwa/global-setup.js (WaiterPwa E2E paketi
    sahipliğinde) — spawn edilen Host sürecine
    `ALKAROS_LOGIN_RATE_LIMIT_PERMITS=1000` eklendi.
  - tests/E2E/WaiterPwa/specs/02-ordering.spec.js, 03-waiter-actions.spec.js
    (WaiterPwa E2E paketi sahipliğinde) — gerçek bir kaynak sızıntısı
    düzeltildi: her iki dosyada da `browser.newContext()` ile açılan
    paylaşılan context, dosyanın hiçbir testinde asla kapatılmıyordu;
    şimdi `test.afterAll` içinde kapatılıyor. (Bu, flake'in kendisini
    DÜZELTMEDİ — doğrulandı — ama gerçek, bağımsız bir hataydı, aynı
    araştırma sırasında bulunduğu için aynı commit'te kapatıldı.)

## Out of scope

- `ALKAROS_LOGIN_RATE_LIMIT_PERMITS`'in başka bir yerden (appsettings,
  CLI argümanı) yapılandırılabilir hale getirilmesi — yalnız ortam
  değişkeni yeterli, E2E'nin kendi ihtiyacı bu kadar.
- Diğer rate-limit politikalarının (`pairing-create`, `terminal-read`,
  `terminal-write`, vb.) benzer bir geçersiz kılma alması — yalnız
  `login` politikası bu flake'in gerçek nedeniydi, diğerleri hiç
  tetiklenmiyordu (Host logları temizdi).
- `V1-WTR-028`'in kendi "Out of scope" notundaki yanlış "makine/CPU
  rekabeti" hipotezinin o dosyada düzeltilmesi — tarihsel bir kayıt
  olarak bırakıldı, bu görev doğru kök nedeni ayrı bir dosyada
  belgeliyor.

## Dependencies

- V1-WTR-028

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Debug` → 0 uyarı, 0 hata.
- `ALKAROS_TEST_PG_PORT=55432 ALKAROS_TEST_PG_PASSWORD=postgres dotnet test`:
  - `tests/Host/MigrationComposition --filter FullyQualifiedName~DualScreen`
    → 49/49 (regresyon — rate-limit testleri dahil, `login` dışındaki
    politikalar hiç değişmedi).
  - `tests/Host/Experience/HelpRequests` → 7/7 (regresyon).
- `tests/E2E/WaiterPwa` (gerçek Chrome + gerçek Postgres + gerçek Host):
  - Düzeltmeden ÖNCE (yalnız context-sızıntısı düzeltmesiyle, rate-limit
    değişmeden): tam paket 3 kez art arda çalıştırıldı, **3/3'ünde de
    aynı test aynı şekilde başarısız** — sızıntı düzeltmesinin flake'i
    KAPATMADIĞINI doğrudan kanıtladı.
  - `E2E_HOST_LOG=1` ile tam paket çalıştırılıp gerçek başarısızlık anı
    incelendi: `POST /api/v1/auth/login - 429` iki kez, tam olarak
    başarısız olan iki context'in login çağrısında.
  - Rate-limit düzeltmesinden SONRA: tam paket **5 kez art arda
    çalıştırıldı, 18/18 her seferinde** (önceden 17/18 idi) — üstelik
    koşum süresi de kısaldı (~56-57s → ~43-47s, artık 15 saniyelik zaman
    aşımını hiç beklemiyor).
- `python tools/consistency-audit/consistency_audit.py` → bu görevin
  değiştirdiği dosyalarda 0 ihlal. Kalan tek ihlal,
  `src/Modules/Inventory/ManualAdjustments/InventoryAdjustmentService.cs:96`,
  bu görevden önce vardı ve sahip olunan yüzeyin dışında.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0
  uyarı.

## Handoff

- None
