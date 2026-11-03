# V1-RMD-787 - Sonuç

Kasa incelemesinin küçük backend kritikleri (bulgular 33, 8, 7, 11, 12).

## Değişiklik

- (a) Ölü UTC hesaplayıcı `CalculateServiceWindow` (arayüz, gerçekleme, `ServiceWindowFilter`) ve testi silindi; iki açıklama bağlantısı yeniden yazıldı.
- (b) Telemetri, tarayıcı hatası ve çevrimdışı kuyruk uçlarına `RequestSizeLimitAttribute` (32 KB) eklendi; parçalı (chunked) gövde artık 413 `PAYLOAD_TOO_LARGE` (katalogda `BadHttpRequestException 413` kolu). Gerçek parçalı istek testleri düzeltmeden önce kırmızıydı.
- (c) PosTerminal `submitOrder`: aynı sipariş + beklenen revizyon için işlem anahtarı yeniden denemede sabit kalır, başarıda silinir (testte düzeltmeden önce 1 kırmızı).
- (d) `pairings/approve` ve `display-sessions/revoke` artık `payments.take` yetkisi ister (kasiyer/süpervizör/yönetici'ye migration 163 verir). Garson `payments.take`siz ikinci ekranı bağlayamaz/kapatamaz (yeni test, düzeltmeden önce kırmızı). Garson ve kasiyer oturumları terminal paylaşıyor; bu yüzden yetki gerekliydi.
- (e) Kart yer tutucu gerekçesinden görev kodu çıkarıldı; hiçbir kod/test bu metni karşılaştırmıyor (yalnız yorum). Test görev kodu desenini yasaklar (düzeltmeden önce kırmızı).
- (f) `CardSettlementOrchestrator`: Unknown kaydından önce, hesap kilidi altında kalan tutar (ayarlanmış ödenecek - tahsisler) hesaplanır; aşan deneme `OverAllocationException` (409 `OVER_ALLOCATION`), hiçbir kayıt yazılmaz. Reddedilen/onaylanan dallar değişmedi (onay zaten tahsis korumasından geçer; ret para kaydı değildir).
- (g) `EftTenderHandler`: aynı işlem anahtarı farklı tutarla gelirse `EftAmountMismatchException`, 409 `TENDER_IDEMPOTENCY_KEY_REUSED` "İşlem kimliği başka bir tutarla zaten kullanılmış."; yeniden oynatma aynı tutarla eskisi gibi.
- (h) Tutar kuralı: pozitif, en fazla 2 ondalık, en çok 9 999 999 999 999 999,99 (NUMERIC(18,2)). `CashTenderRequest` (tutar ve verilen), `TenderRequest.Amount`, `CashTransaction` kurucusu (kasa hareketleri). `InvalidCashTransactionAmountException` önceden eşlenmediği için 500 dönüyordu; katalogda 400 `VALIDATION_FAILED` Türkçe mesaj, `amountDue`/`tenderedAmount` etiketleri eklendi.
- (i) `CompleteManualAsync`: durum denetimi fiş numarası doğrulamasından önce (bekleyen olmayan iade boş fişle artık 409 `REFUND_STATE`, 400 değil).

## Doğrulama

Gerçek Postgres (55432). Atlanan test yok. Klasör `evidence/V1-RMD-787/`:
- Yeşil: Reporting.V1Operations 5, Errors 210, TenderComposition 9, CardSettlement 10, PaymentTender 64 (`r2-paytender`), Cash.TransactionLedger 33, Cash.TenderHandler 13, Payments.TenderRouting 31, Refund HTTP 42, CashSession HTTP 27, DualScreenAuthorizationHttpTests 34, ModuleBoundaries 17, ApiConventions 17, CustomerAccounts.CashReceipts 11; PosTerminal vitest 839/839, tsc 0, lint 0 (yalnız eski uyarılar).
- Kırmızı kanıtı: (b),(c),(d),(e) düzeltmeden önce gerçekten kırmızı koşuldu (`red-*`); (f),(g),(h),(i) için kırmızı, düzeltmeyi geri alan mutantla gösterildi (`m-*`).
- Mutasyon (hepsi `cmp` ile geri alındı): kalan tutar denetimi kapalı (1 kırmızı), EFT tutar karşılaştırması kapalı (1), ölçek/sınır denetimi kapalı CashTransaction (3) / CashTenderRequest (4) / TenderRequest (2), katalog kolu silindi (1), iade durum sırası bozuldu (1).

## İncelemeler (sırayla)

- qa-backend: yüksek/kritik yok. Giderilenler: kart isteğinin tutar ölçeği/sınırı (`CardSettlementRequest`, onaylı tutar dahil), kasa oturumu açılış/sayım/kapanış tutarı ölçeği (`CashSessionCommands`, sıfır serbest; plan eki `c17371da`), `RejectAsync` doğrulama sırası (durum önce), `api.ts` yorum yeri ve 4xx sonrası anahtar silme.
- qa-test-kalitesi: giderilenler: vergi/bahşiş ile yükselen tavanda Unknown testi ve tam sınır (30,01) ile kayıt sayısı, kart onaylı tutar sınırları (0, negatif, ölçek, üst sınır kabulü), ağ hatasında anahtarın korunması (bu test `status 0`ı silen gerçek bir hatayı yakaladı; düzeltildi: yalnız 400-499 siler), testler arası anahtar yalıtımı, oturum/tahsilat tutar alanı adlı HTTP testi. Mutasyonlar: kalan tutar tavanı bahşişsiz hesap (kırmızı), kart/oturum ölçek denetimi (2/2), `RejectAsync` sırası (1), 4xx anahtar silme (1).
- Kabul edilip değişmeyenler: terminal belirsiz çekim yaptığında kalan tutarı aşan kayıt reddi (bulgu f gereği; cihaz bağlantısı henüz yer tutucu, tetiklenmez); telemetri için meta veri testi (şema yok, parçalı davranış iki kardeş uçta kanıtlı); tutar kuralının üç kopyası (modül sınırı ortak tipi yasaklar); `HUG-1` gibi başka kod biçimi regex dışı.

## Açık risk / ayrı karar

- HSTS/vekil konumu ve Google Fonts dış isteği (`PosTerminal/index.html:7-18`) bu görevde yok; ayrı karar.
- Terminal belirsiz çekim yapıp tutar kalanı aşarsa kayıt reddedilir (iz yok); gerçek cihaz entegrasyonu gelince ayrıca kararlaştırılmalı.
- İstemcilerin tutarı 2 ondalığa yuvarlayıp yuvarlamadığı tüm ekranlarda taranmadı; yuvarlanmamış çağrı artık 400 alır.
