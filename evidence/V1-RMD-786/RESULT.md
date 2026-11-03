# V1-RMD-786 - Sonuç

Hata eşleme: alan belirten doğrulama mesajı, iyimser kilit çakışması 409, eşleştirme kodu hatası, indirim aşımı mesajı.

## Değişiklik

- `ApiErrorCatalog`: her `ArgumentException` cevabı `ValidationAnswer` üzerinden geçer (14 yer, Host varsayılanı dahil). `ParamName` Türkçe alan sözlüğündeyse "İndirim tutarı geçersiz veya eksik." / "Adet izin verilen aralığın dışında." gibi alan ve neden söylenir; ham istisna metni sızmaz; bilinmeyen alan eski genel cümleyi alır. `ArgumentException.ThrowIfNullOrWhiteSpace(request.IdempotencyKey)` (kasa hareketi) "request.IdempotencyKey" adını üretir, son parça sözlükte aranır.
- Billing grubu: "Hesap bölme isteği doğrulanamadı." kalktı. İndirim aşımı `DiscountExceedsPayableException` (400 `DISCOUNT_EXCEEDS_PAYABLE`, "İndirim toplamı hesap tutarını aşıyor."). Kalan `InvalidOperationException` hâlâ 400 (toplu 409 yok): "İstek hesabın şu anki durumuyla uyumlu değil."
- İyimser kilit: `BillConcurrencyException`, `CashSessionConcurrencyException`, `OrderConcurrencyException`, `PaymentConcurrencyException` (hepsi `InvalidOperationException`dan türer; modül sınırı ortak tipi yasaklar). Sekiz atış yeri: PostgresBillRepository (2), PostgresCashSessionRepository (1), PostgresOrderRepository (2), PostgresPaymentRepository, PostgresRefundIntentRepository, PostgresManualCardConfirmationRepository. Katalog `Default` ve Billing `Map` 409 `CONCURRENCY_CONFLICT`.
- Eşleştirme kodu: `NormalizeCode` alfabe dışı karakter, yanlış uzunluk, boş kod için `DualScreenPairingException.InvalidCode()` (404 `PAIRING_CODE_INVALID`) atar; onay ucundaki "bulunamadı" ile aynı tek üretici.
- ApiConventions: `ApiErrorMappingTests` (400/404/409 ve kararlı kod), `EveryRefusalCarriesTheErrorEnvelope` (her yetki hatası cevabı `code`+`message` taşır; `status`+`traceId` olmayan 68 uç `api_convention_allowlist.json` `refusalWithoutStatusAndTrace` bölümünde küçülür-yalnız listede, görev V1-RMD-431), `AnUnroutableCallAnswersWithTheEnvelope` (404 ve 405 tam zarf).

## İstemci denetimi (değişiklik gerekmedi)

- `src/Clients/Cashier/wwwroot/cashier-app.js:783-786`: 409 `CONCURRENCY_CONFLICT` yeniden denenebilir sayılır; sipariş yolundaki çakışma eskiden 500 idi, artık bu dala düşer.
- `src/Clients/PosTerminal/src/routes/Cashier.tsx:359-385`: `ApiError` 409 ise "conflict" geri bildirimi ve yeniden yükleme, ileti sunucunun Türkçe mesajıdır.
- Eşleştirme penceresi aynı `execute` ile sunucu mesajını gösterir (`Cashier.help-alerts.test.tsx:388-395`, `PAIRING_CODE_INVALID`).
- `cash-session.js:86` ve `split-payment.js:124`: 409 için "sayfayı yenileyip tekrar deneyin" metni durum koduna bağlıdır, yeni 409 oraya düşer.

## Doğrulama

Gerçek Postgres (port 55432). Not: depo ve eşleştirme testlerinde kırmızı, düzeltmeden önceki bir koşu olarak değil, düzeltmeyi geri alan mutantlarla gösterildi; katalog testleri düzeltme öncesi gerçekten kırmızıydı (`red-errors/`: 7 kırmızı).

- Yeşil: Errors 208/208, Billing.BillFoundation 50, Orders.OrderAggregate 159, Cash.SessionLifecycle 16, Payments.PaymentAggregate 51, DualScreenAuthorizationHttpTests 33, ApiConventions 17, Billing host tüm proje ve PaymentTender (`f-*`), Billing.Adjustments 21, BillingSplitErrorMappingTests 10; ek koşular: CashSession HTTP 25, RefundIntents 13, ModuleBoundaries 17, Cash.Contracts 14, TransactionLedger 27, SubmitOrder 18, ItemExceptions 22, PaymentClosure 24.
- Mutasyon (hepsi `cmp` ile geri alındı): sözlük araması (3 kırmızı), Bill/Cash/Order/Payment atışını eski istisnaya çevirmek (2/1/1/1 kırmızı), Billing çakışma kolu (3), indirim aşımı kolu (1), katalog 409 kolu (4), `NormalizeCode` eski `ArgumentException` (4, DualScreen HTTP).
- Atlanan test yok.

## Bulundu, kapsam dışı

- `OrderManagementEndpoints.cs:1225` her `InvalidOperationException`ı 409 `CONCURRENCY_CONFLICT` yapar (toplu çevirme); bu görevin yazma yüzeyi dışında.
- Birçok uç (68) yetki hatasında `status`/`traceId` taşımıyor; liste artık küçülür-yalnız.

## İncelemeler (sırayla)

- qa-backend: kaçırılmış atış yeri bulunamadı (Billing/Cash/Orders/Payments); Tables, Kitchen ve Stok/Reçete/Yazıcı modüllerinin kendi düz `InvalidOperationException` atışları kasa yolu dışında olduğu için kapsam dışı bırakıldı (aşağıda açık risk). `OrderManagementEndpoints.cs:1219` toplu `InvalidOperationException` -> 409 satırı bilerek değişmedi: sipariş etki alanı durum hatalarını (`Order.cs`) yazılı tiplerle değil düz `InvalidOperationException` ile atıyor; satırı daraltmak bunları 500 yapardı. Gidermeler: elle kart onayı ve iade niyeti için bayat sürüm testleri eklendi (mutasyonla doğrulandı), sipariş kalemi için `StaleItemRowVersionSaveRejected` eklendi (mutasyonla doğrulandı), `IManualCardConfirmationRepository` açıklaması yeni istisnayı söyler.
- qa-test-kalitesi: `ApiErrorMappingTests` artık tam mesajı da doğrular ve `CashSessionNotFoundException` gerçek kurucuyla üretilir; Billing `InvalidOperationException` mesajı ayrı testle sabitlendi; `CustomerCreditTerms` dışlaması gerekçeli yorum aldı.
- Kabul edilip değişmeyenler: Billing grubunda `CashSessionConcurrencyException` eşlemesi yok (Billing akışı Cash tipi atamaz); `BillingSplitStore.cs` `nameof(request)` ile atan üç `ArgumentException` genel cümleye düşer (CA2208 alan adı vermeye izin vermiyor, Out-of-scope); sözlükteki "Oran" ve "Not" etiketlerinin başka modülde çakıştığı bir atış yeri bulunmadı.

## Açık risk

- Tables (`PostgresTableRepository`), Kitchen (`PostgresKitchenTicketRepository`), Stok, Reçete ve Yazıcı depoları hâlâ düz `InvalidOperationException` ile çakışma bildirir; Tables/Kitchen kendi Host depolarında çevriliyor, ulaşılabilirliği doğrulanmadı.
- `OrderManagementEndpoints.cs:1219` toplu çeviri.
- 68 yetki hatası cevabında `status`/`traceId` yok (küçülür-yalnız liste).
