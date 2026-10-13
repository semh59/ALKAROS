# V1-RMD-502 - Masa ve bölge düzenleme, bölge silme ekranı

## Kurallar

- Masa düzeni ekranında yöneticiye (`manage` yetkisi ve ilgili işleyici varsa) iki yeni düğme açıldı:
  - **Masayı düzenle:** seçili masanın numarası, bölgesi, kapasitesi ve etkinliği güncellenir. Boş numara veya sıfır kapasite sunucuya gitmeden reddedilir.
  - **Bölgeleri yönet:** bölge adı, sırası ve etkinliği güncellenir; bölge "Sil" ile silinir ve silme ayrı bir onay adımı ister. Bölgede masa varsa sunucunun Türkçe reddi gösterilir.
- Tüm güncelleme ve silmeler kaydın güncel satır sürümünü gönderir; eşzamanlı değişiklikte "Kayıt başka biri tarafından değiştirildi. Listeyi yenileyip tekrar deneyin." gösterilir ve pencere açık kalır. Ham sunucu metni veya durum kodu gösterilmez.
- `FloorPlanWorkspace` "Birleşimi ayır" düğmesi değiştirilmedi: yalnız sunucu birleşim ana masası için `Unmerge` komutunu vermediğinde kapalı gösterilir; komut verildiğinde zaten çalışıyor (bağımsız taramadaki bulgu yanlış okumaydı).

## Kanıt

- `tests-client.log`: tüm istemci 64 dosya / 492 test geçti (yeni: arayüzde 5, istemci yol/gövde sözleşmesinde 1). `typecheck.log`, `oxlint.log`, `build.log`: exit 0.
- `mutation.log`: 8 mutant (satır sürümü gönderimi, silme onayı, boş numara, yetki, çakışma metni, sunucu reddi), hepsi en az bir testi kırdı; dosyalar yedekten geri yüklendi.
- Gerçek-Host denemesi yapılmadı: sunucu uçları değişmedi; istemci–sunucu sözleşmesi (`UpdateZoneRequest`, `UpdateTableRequest`, `DELETE ...?expectedRowVersion`) yol/gövde testiyle sabitlendi.

## Açık kalan

- Masa güncellemede etkin/pasif yapma, masada açık sipariş varken sunucu kuralına bağlıdır; ekran sunucunun reddini olduğu gibi (Türkçe) gösterir.
- Masa silme ucu sunucuda yok; masa yalnız pasife alınabilir.
