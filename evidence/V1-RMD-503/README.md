# V1-RMD-503 - Tedarikçi bilgisi düzenleme ekranı

## Kurallar

- Satın alma > Tedarikçiler tablosunda her satıra "Düzenle" eklendi: tedarikçinin güncel ayrıntısı sunucudan okunur, ad, vergi numarası, vergi dairesi, telefon ve e-posta düzenlenir. Boş alan boş değer (null) olarak gönderilir; boş ad sunucuya gitmeden reddedilir.
- Sunucu rol nedeniyle maskeli veri döndürdüyse (`isMasked`) düzenleme açılmaz ve "Bu oturumun yetkisi tedarikçi bilgilerini düzenlemeye yetmiyor." gösterilir; böylece maske değeri (`******7890` gibi) gerçek kaydın üzerine yazılamaz.
- Sunucunun Türkçe reddi (ör. aynı vergi numarası) olduğu gibi gösterilir; ham durum kodu veya İngilizce metin gösterilmez.

## Kanıt

- `tests-client.log`: tüm istemci 64 dosya / 496 test geçti (yeni: 4 test — güncel ayrıntıyla düzenleme, maskeli veri reddi, boş ad ve sunucu reddi, istemci yol/gövde sözleşmesi). `typecheck.log`, `oxlint.log`, `build.log`: exit 0.
- `mutation.log`: 5 mutant (maske kontrolü, boş ad, boş değer, güncel ayrıntı, yol), hepsi en az bir testi kırdı; dosyalar yedekten geri yüklendi.
- Gerçek-Host denemesi yapılmadı: sunucu uçları değişmedi; sözleşme yol/gövde testiyle sabitlendi.

## Açık kalan

- Tedarikçi kodu değiştirilemez (sunucu sözleşmesinde yok).
- Maskeli rolle açılan oturumlar tedarikçi düzenleyemez; bu bilinçli (V0-CMP-003 veri minimizasyonu).
