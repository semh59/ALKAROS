# V1-RMD-786 - Hata eşleme: alan belirten doğrulama mesajı, iyimser kilit çakışması 409, eşleştirme kodu hatası

- Task ID: V1-RMD-786
- Status: InProgress
- Assignee: claude-code-session_019vXeYXjE3sM6VZKpRMj1L9
- Work type: implementation
- Surface state: Existing

## Goal

Kasa incelemesinin 15. bulgusu (koddan doğrulandı) üç kusuru birleştirir:

1. `ApiErrorCatalog` her `ArgumentException` için, hangi alanın bozuk olduğunu söylemeden tek cümle ("İstek doğrulanamadı.") döner (katalogda 14 yerde); Billing grubu ayrıca indirim gibi alakasız hatalara bile "Hesap bölme isteği doğrulanamadı." der. Düzeltme: özel durumun `ParamName` değeri bilinen bir alan sözlüğündeyse mesaj alanı ve nedeni (eksik/geçersiz ya da aralık dışı) söyler; ham istisna metni hiçbir zaman istemciye sızmaz; sözlükte olmayan alan eski genel cümleyi alır.
2. İndirim toplamı hesap tutarını aşınca `AdjustmentCalculator` düz `InvalidOperationException` atar ve Billing grubu bunu "Hesap bölme isteği doğrulanamadı." der; özel `DiscountExceedsPayableException` (400, `DISCOUNT_EXCEEDS_PAYABLE`, "İndirim toplamı hesap tutarını aşıyor.") olur.
3. İyimser kilit (row_version) çakışması bugün `InvalidOperationException` olarak atılır: Billing grubunda 400, kasa oturumu ve sipariş yolunda 500. Düzeltme: her kasa modülü (Billing, Cash, Orders, Payments) kendi çakışma istisnasını tanımlar (modül sınırı farklı derlemeler arası tip paylaşımını yasaklar), depolar onu atar, katalog ve Billing eşlemesi 409 `CONCURRENCY_CONFLICT` döner. Yeni istisnalar `InvalidOperationException`dan türer; `InvalidOperationException` toplu olarak 409'a çevrilmez (`BillingSplitApplication` programlama hataları için de atar).
4. Müşteri ekranı eşleştirme kodunda 0/O/1/I gibi alfabe dışı karakter ya da yanlış uzunluk `ArgumentException` (400) üretir; `PAIRING_CODE_INVALID` (404) dönmelidir.

Ayrıca ApiConventions testleri hata zarfını (code, message, status, traceId) ve 400/404/409 eşlemelerini denetler (rapor 5-B13: bugün denetlenmiyor).

İstemciler: PosTerminal ve Cashier hata mesajını sunucunun Türkçe mesajından ya da durum kodundan (409) gösterir; etkilenen yerler kodla doğrulanır.

## Owned surface

- `src/Modules/Billing/BillFoundation/BillConcurrencyException.cs`
- `tests/Architecture/ApiConventions/ApiErrorMappingTests.cs`
- `evidence/V1-RMD-786/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Modules/Cash/Contracts/CashSessionConcurrencyException.cs (V1-CSH-001) — yeni dosya: kasa oturumu çakışma istisnası.
  - src/Modules/Orders/OrderAggregate/OrderConcurrencyException.cs (V1-RMD-064) — yeni dosya: sipariş çakışma istisnası.
  - src/Modules/Payments/PaymentAggregate/PaymentConcurrencyException.cs (V13-PAY-001) — yeni dosya: ödeme çakışma istisnası.
  - src/Modules/Billing/Adjustments/DiscountExceedsPayableException.cs (V1-BIL-003) — yeni dosya: indirim toplamı hesap tutarını aşıyor.
  - src/Modules/Billing/Adjustments/AdjustmentCalculator.cs (V1-BIL-003) — aşımda özel istisna.
  - tests/Modules/Billing/Adjustments/AdjustmentsDomainTests.cs (V1-RMD-467) — aşım istisnasının tipi.
  - src/Modules/Payments/ManualResolution/IManualCardConfirmationRepository.cs (V1-RMD-283) — sözleşme açıklaması yeni istisnayı söyler.
  - tests/Modules/Payments/Allocations/RefundIntents/PostgresRefundIntentRepositoryTests.cs (V1-RMD-298) — iade niyeti bayat sürüm testi.
  - tests/Host/Experience/PaymentTender/PaymentTenderHttpTests.cs (V1-RMD-258) — elle kart onayı bayat sürüm testi.
  - src/Host/Composition/Errors/ApiErrorCatalog.cs (V1-RMD-431) — alan sözlüğü, doğrulama cevabı, çakışma cevabı.
  - src/Host/Experience/Billing/BillingSplitApplication.cs (V1-RMD-103) — Billing eşlemesi: çakışma 409, doğrulama cevabı.
  - src/Modules/Billing/BillFoundation/PostgresBillRepository.cs (V1-RMD-414) — çakışma istisnası.
  - src/Modules/Cash/SessionLifecycle/PostgresCashSessionRepository.cs (V1-RMD-416) — çakışma istisnası.
  - src/Modules/Orders/OrderAggregate/PostgresOrderRepository.cs (V1-RMD-108) — çakışma istisnası.
  - src/Modules/Payments/PaymentAggregate/PostgresPaymentRepository.cs (V13-PAY-001) — çakışma istisnası.
  - src/Modules/Payments/Allocations/RefundIntents/PostgresRefundIntentRepository.cs (V1-RMD-311) — çakışma istisnası.
  - src/Modules/Payments/ManualResolution/PostgresManualCardConfirmationRepository.cs (V1-RMD-283) — çakışma istisnası.
  - src/Host/DualScreen/DualScreenStore.cs (V1-RMD-035) — eşleştirme kodu doğrulaması.
  - src/Host/DualScreen/DualScreenStore.Display.cs (V1-RMD-097) — eşleştirme kodu hatası tek yerden.
  - src/Host/DualScreen/DualScreenExceptions.cs (V1-RMD-097) — geçersiz kod hatası üretici.
  - src/Host/DualScreen/DualScreenApplication.CashSession.cs (V13-CSH-004) — işlem anahtarı hatasının alan adı.
  - tests/Host/Errors/ApiErrorCatalogTests.cs (V1-RMD-431) — alan mesajı ve çakışma eşlemesi.
  - tests/Host/Experience/Billing/BillingSplitErrorMappingTests.cs (V1-RMD-106) — Billing grubunda çakışma 409 ve alan mesajı (HTTP ile üretilemez: her Billing yazımı önce satır kilidi alır).
  - tests/Host/MigrationComposition/DualScreen/DualScreenHostTests.cs (V1-GOV-008) — alfabe dışı kod PAIRING_CODE_INVALID.
  - tests/Modules/Billing/BillFoundation/PostgresBillTests.cs (V1-RMD-002) — eski istisna tipi beklentisi.
  - tests/Modules/Orders/OrderAggregate/PostgresOrderTests.cs (V1-RMD-108) — eski istisna tipi beklentisi.
  - tests/Modules/Payments/PaymentAggregate/PostgresPaymentTests.cs (V1-RMD-230) — eski istisna tipi beklentisi.
  - tests/Modules/Cash/SessionLifecycle/CashSessionLifecycleServiceTests.cs (V1-RMD-417) — kasa oturumu çakışma istisnası.
  - tests/Architecture/ApiConventions/ApiRouteAuthorizationTests.cs (V1-RMD-429) — hata zarfı denetimi.
  - tests/Architecture/ApiConventions/api_convention_allowlist.json (V1-RMD-429) — status ve traceId taşımayan 68 yetki hatası cevabının küçülür-yalnız listesi.
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. `ArgumentException` cevabı: alan sözlüğü (hesap, sipariş, masa, ödeme, tutar, indirim tutarı, gerekçe, işlem anahtarı, para birimi, adet, kişi sayısı, terminal, kasa oturumu, oran, bölme hedefleri vb.); `ArgumentOutOfRangeException` aralık dışı, diğerleri geçersiz ya da eksik; bilinmeyen alan genel cümle.
2. Dört çakışma istisnası, sekiz atış yeri (Billing 2, Cash 1, Orders 2, Payments 3); katalog `Default` ve Billing `Map` 409 `CONCURRENCY_CONFLICT`.
3. `NormalizeCode` geçersiz kodda `DualScreenPairingException` (`PAIRING_CODE_INVALID`, 404) atar.
4. ApiConventions: hata zarfı (401, 404, 405 cevapları) ve eşleme sözleşmesi (400 alan mesajı, 404, 409 çakışma).
5. Her düzeltme için önce kırmızı test, sonra yeşil.

## Out of scope

- `InvalidOperationException` genel eşlemesi (Billing'de mevcut 400 olduğu gibi kalır; yalnız çakışma istisnası 409 olur); diğer alanların (Katalog, Menü, Satın alma vb.) kendi çakışma istisnaları (zaten 409).
- Kasa oturumu/ödeme sayfası hata ekranları (kasa incelemesi 16. bulgu).

## Dependencies

- V1-RMD-785

## Acceptance evidence

1. Kırmızı sonra yeşil: katalog, Billing HTTP, DualScreen HTTP, depo testleri gerçek Postgres'te; ApiConventions yeni testleri.
2. Mutasyon: sözlük araması, çakışma eşlemesi, kod doğrulaması bozulunca testler kırılır; kaynak `cmp` ile geri alınır.
3. ApiConventions ve ModuleBoundaries testleri yeşil; plan audit ve consistency audit 0 hata/0 uyarı; kapsam aracı exit 0.
4. qa-backend ve qa-test-kalitesi incelemeleri sırayla yapılır, bulgular giderilir.
5. Semih'in elle deneyeceği senaryo: kasada aynı hesaba iki sekmeden dokunup ikincisinde "çakışıyor, sayfayı yenileyin" mesajını görmek; müşteri ekranı eşleştirmede "0" içeren kodun "kod yanlış" demesi.

## Handoff

- None
