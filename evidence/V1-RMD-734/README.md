# V1-RMD-734 - Cashier istemci düzeltmeleri (603 B007, B032, B033, B036)

**Sonuç:** Kasa ödeme sayfalarında sunucu hatası/ağ kesintisi artık oturum düşmesi gibi görünmüyor, aynı işlem tekrar gönderilince çift tahsilat olmuyor, ekran takılı kalmıyor ve odak/dokunma sorunları giderildi. 10/10 mutasyon KILLED, StaticApps 135/135.

- **B032 (hata dayanıklılığı):** üç sayfada da her istek 15 sn zaman aşımlı. Açılışta oturum kontrolü 500/ağ hatası verirse "Oturumunuz sona erdi" yerine tekrar dene ekranı; yalnız 401/403 giriş ekranına gider. İşlem anahtarı (tahsilat, indirim, bahşiş, kasa hareketi, cari tahsilat) sunucu 5xx verince saklanır, yeniden denemede aynı anahtar gider; kesin yanıtta (2xx/4xx) yenilenir. Cari hesaplarda eski arama/ekstre yanıtı yenisini ezmiyor.
- **B033 (akış):** eşit bölmede her tahsilattan sonra sıradaki kişinin payı hazır gelir, son kişi kalanı öder (0,01 artığı yok). 5 sn'lik onay yoklaması imleci/odaklı alanı korur.
- **B036 (erişilebilirlik/saat):** tutar alanlarında klavye odak halkası (`:focus-within`), ödeme yöntemi çipleri 48 px, onay kutusu 32 px; saatler cihaz dilimi yerine İstanbul saatiyle.
- **B007 (kilit uyarısı):** kart kilidi uyarısı terminalde onay görülmediyse tahsil sayılmadığını ve mutabakatı yalnız müdürün tamamlayabildiğini söylüyor.

**Yapılmayan:** kilit uyarısında tutar gösterimi (sunucu `unsettledPayment` içinde tutar vermiyor; backend yüzeyi dışı). Müdür alanlarını yetkisiz kullanıcıdan gizleme (istemcide yetki bilgisi yok, sunucu zaten reddediyor).

## Kanıt
`logs/vitest.log` (135/135, yeni `cashier-client-resilience.test.js` 14 test), `mutations.log` (10/10 KILLED).
