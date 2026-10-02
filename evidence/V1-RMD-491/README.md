# V1-RMD-491 - Alış faturaları yönetim ekranı

Yönetim ekranına "Alış faturaları" bölümü eklendi (`purchasing.manage` yetkisi). V1-RMD-489/490 uç noktalarını kullanır.

- XML dosyası yüklenir (dosya seçici), taslak olarak alınır ve açılır.
- Liste duruma göre süzülür (varsayılan Taslak); eşleşmemiş satır sayısı görünür.
- Taslak faturanın her satırı için hammadde ve çevrim katsayısı seçilip "Eşleştir"e basılır; sunucu eşleştirmeyi tedarikçi için hatırlar.
- "Onayla ve stoğa gir" yalnız tüm satırlar eşleşmiş ve giriş deposu seçilmişse açılır; "Reddet" taslağı kapatır. Onaylı veya reddedilmiş faturada işlem düğmesi yoktur.
- Kayıtlı tedarikçisi olmayan fatura için uyarı gösterilir. Sunucunun Türkçe hata nedeni olduğu gibi gösterilir.
- Yazma çağrıları `idempotencyKey` gönderir.

## Kanıt

- `tests-ui.log`: tüm arayüz testleri geçti (yeni bölümün 10 testi dahil).
- `typecheck.log`: tsc çıkış kodu 0. `lint-build.log`: oxlint hatasız, vite build başarılı.
- `mutation.log`: 8 mutant (onayın eşleşmemiş satırla veya depo seçmeden açılması, sıfır katsayı, hammaddesiz eşleştirme, onaylı faturada düğmeler, tedarikçi uyarısı, boş XML gönderimi, ham durum metni) hepsi yakalandı; dosya `cmp` ile geri yüklendi.
- Uç noktaların gerçek Host denemesi `evidence/V1-RMD-489` ve `evidence/V1-RMD-490` altındadır; istemcinin adres ve gövdesi testle sabitlendi.
