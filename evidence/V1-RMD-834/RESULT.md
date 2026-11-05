# V1-RMD-834 sonuç

Kapatılan: (1) Katalog fiyat formunda "Geçerlilik başlangıcı" cihazın saat dilimiyle dolduruluyor ve gönderiliyordu; artık işletme saati (Europe/Istanbul) gösteriliyor, yazılan değer aynı saniyeye karşılık gelen UTC anına çevriliyor (`venueDateTimeInput`, `venueInstant`, `format.ts`). (2) Kanal raporu ve ürün marjı ekranları "Günler işletme gününe göre sayılır: gün sabah 06:00'da başlar" ipucunu, sipariş faturaları ekranı "Hizmet tarihi takvim günüdür" ipucunu gösteriyor.

## Doğrulama
- vitest 101 dosya, 1073/1073 yeşil; tsc 0 hata. Yeni/değişen testler: format (3 saat dilimi), katalog varsayılanı (Los_Angeles cihazda 13:00), katalog gönderimi (10:00Z), üç ipucu.
- Kırmızı: yardımcılar yokken ve eski davranışta testler düştü.
- Mutasyon 6 mutant (gönderim ofseti, gösterim ofseti, gönderimde eski çağrı, varsayılanda eski çağrı, iki ipucu metni) hepsi öldürüldü, dosyalar `cmp` ile geri doğrulandı.

## İncelemeler (qa-frontend, qa-test-kalitesi)
Giderilen: katalog testinde saat dilimi sızıntısı (afterEach geri yükler), `venueInstant` saniyeli girdide RangeError (ilk 16 karakter alınır, testli), fatura ipucundaki "00:00 - 24:00" ifadesi sadeleşti. Kabul edilen: ipucu testleri metin parçası arar (davranış sunucuda doğrulanır); boş girdiye form doğrulaması zaten izin vermez.
