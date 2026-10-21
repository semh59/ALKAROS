# qa-bulgu-dogrulayici — özet (SHA eb93ec69; kod okuması, test koşulmadı)

35 kalem (R1-R17 + R12a-g + TK-B1..B14). Tamamen çürütülen 2 (TK-B9 → R16'ya ölü kolon, UX-B12 sızıntı yok). Kritik yok, Yüksek yok (R1/R2/R3 önerilen önem Orta ama Semih senaryosu bugün sağlanmıyor).

| Kök | Sonuç | Önem | Not |
| --- | --- | --- | --- |
| R1 | DOĞRULANDI | Orta | Masa açma (OrderManagementEndpoints.cs:289-304) ve toplu devir (:966) sınıra bakmıyor; tek tüketici SuggestedWaiterResolver.cs:63,112 |
| N1 (yeni) | DOĞRULANDI | Orta | SuggestedWaiterResolver.cs:71-73 masa değil sipariş sayıyor ve Served'ı dışlıyor; etiket "açık masa" diyor (models.ts:13). Önce bu düzelmeli |
| R2 | DOĞRULANDI | Orta | İlk okuma kendini kaydediyor; kayıt yoksa PUT 404; istemci yalnız ad ve renk yazıyor (api.ts:371). R4'ten önce ekran açılırsa süre 0 yapılabilir |
| R3 | DOĞRULANDI | Orta | SettingsHistorySection.tsx:20, SettingsManagementContracts.cs:27 |
| R4 | DOĞRULANDI | Orta (yalnız elle API) | SettingValidator.cs:74-83,122-131; QrOrderExpiryHostedService.cs:139 süre ≤0 ise bekleyen tüm QR siparişleri reddedilir |
| R5 | DOĞRULANDI | Orta (yalnız API DELETE) | pasif ayara PUT, yeniden etkinleştirme yok |
| R6 | KISMEN | Düşük | sonuç 500 değil 409/503, ilk açılışta bir kez |
| R7 | DOĞRULANDI | Orta | /branding ve /logo tam baytı okur; IP başına hız sınırı var |
| R8 | DOĞRULANDI | Orta | logo aktör/geçmiş yok |
| R9 | DOĞRULANDI | Düşük | CASCADE, silme yolu yok; 5000 koruması gerçekçi değil; ikincil sıralama |
| R10 | DOĞRULANDI | Düşük | N+1, ölçüm yok |
| R11 | KISMEN | Düşük | Host TryAdd'leri ölü kod, captive riski yok |
| R12a-c,e-g | DOĞRULANDI | Düşük | istemci |
| R12d | KISMEN | Düşük | SellerProfileCard dilim dışı |
| R13 | DOĞRULANDI | Düşük | izin sabiti yok |
| R14 | DOĞRULANDI | Düşük | testsiz ayarlar, DELETE hata yolları, 136 down, axe |
| R15 | DOĞRULANDI | Düşük | yalnız kodla kayıt, bugün tetiklenmez |
| R16 | KISMEN | Düşük | 136 sertleştirme; indeks iddiası EXPLAIN olmadan kesin değil |
| R17 | DOĞRULANDI | Doküman sapması | settings-ownership.md:21,26,27; SET-002..006 evidence yok |
TK: doğrulandı B1,B2,B3,B4,B5,B6,B7,B11,B12,B14; KISMEN B8,B10,B13; çürütüldü B9.
TK-B2: imza testi sıfır baytlı dosya kullanıyor, boyut kuralı silinse de geçer. TK-B3: test ChangedBy'ı denetlemiyor.
Düzeltme sırası: N1 → R1; R4 → R2 (ekran aralık denetiminden sonra).
Doğrulanamayan: çerez SameSite/CSRF, logo yanıtında CSP/nosniff, business.name kaçışlaması QR sayfasında, mutasyon yeniden koşusu, indekslerin EXPLAIN'i.
Ajan başına çürütme: test-kalitesi 1, ux 1, diğerleri 0 (KISMEN: backend 3, performans 2, veritabanı 2, mimari 1, frontend 1, api 1, doküman 1).
