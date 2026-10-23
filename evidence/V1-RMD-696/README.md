# V1-RMD-696 - Hız sınırı, kimlik, hub oturumu ve sertleştirme

## Ne değişti

- **Hub oturumu (B016):** açılan her hub bağlantısı (müşteri ekranı, garson durumu, yardım çağrısı, düşük stok) kaydedilir; 30 sn'de bir aynı oturum kontrolü tekrarlanır ve iptal edilmiş oturumun ya da pasifleştirilmiş kullanıcının bağlantısı düşürülür. Kontrol yapılamazsa (veritabanı yok) bağlantı korunur ve uyarı yazılır; tarayıcı turundaki beklenmeyen hata sunucuyu durdurmaz.
- **Ekran koruyucu yükleme (B031):** dosya artık yetki filtresinden sonra okunuyor (önceden gövde yetkiden önce çözülüyordu). Bozuk gövde yetkisiz çağrıda 401 veriyor.
- **Hız sınırı (B032, B034):** politikası olmayan her `/api` ucu adres başına dakikada 1200 ile sınırlı (yönetim, mutfak, fatura dahil). QR sipariş uçları oturum başlığı ne olursa olsun adres başına 300 ile sınırlı (başlık değiştirerek aşılamıyordu).
- **Giriş sınırı (B033):** ortam değişkeniyle verilen değer en fazla 600 olabilir; "tek kova" iddiası yalnız güvenilir vekil yokken doğru, gönderilen yapılandırmada vekil tanımlı.
- **Sağlık yanıtı (B079):** kayıt satırına ham iletinin yerine yalnız hata türü ve SQLSTATE yazılıyor; çıktı her zaman geçerli JSON.
- **Elle bakım işi (B064):** çalıştıran yöneticinin kimliği günlüğe ve (imha işinde) denetim olayının korelasyon kimliğine yazılıyor.
- **`--db-url` parolası (B103):** parola içeren URL göç komutunda reddediliyor; psql parolası artık yalnız ortam değişkeninden geliyor.
- **Sır içeren kayıtlar (B116):** `DualScreenOptions`, `HostDatabaseConnection`, `PsqlOptions` yazdırıldığında bağlantı bilgisi görünmüyor.
- **Sertifika dosyası (B117):** kendinden imzalı PFX Unix'te baştan yalnız sahibine açık kipte oluşturuluyor.
- **Fiş ve tahsilat özeti (B078, PO kararı: kasiyer ve yönetici):** `payments.take` yetkisi şart; yetkisiz oturum 403.
- **Platform adresi (B080, kapsam genişletildi):** yerel, özel ağ, tek etiketli ve `.local/.internal/.lan` adresleri reddediliyor.

## Kanıt

- `mutate.mjs` / `mutations.log`: 38 mutant; tümü KILLED. İlk geçişte T6 (düşük stok hub'ında bağlantı bırakma) sağ kaldı, test eklendi. S6 ve J1 derleme hatası olarak etiketlendi: S6 derlenir bir mutantla yeniden koşuldu (KILLED), J1 elle koşuldu (1 test başarısız, KILLED); betik etiketi yanlış, kaynak her seferinde geri yüklendi.
- Testler: `AccessHardeningTests`, `LowStockAlertHubSessionTests`, hub iptal testleri (müşteri ekranı, garson, yardım), sağlık, bakım işi, fiş yetkisi, platform adresi (44 satır), `ProgramArgumentTests`.
- Tam `Host.Tests` ve ilgili proje koşuları `logs/` altında.

## Yapılmayanlar / doğrulanamadı

- **B035/B088 (terminal kimliği):** PO kararı "tarayıcıya bağlı gizli anahtar + yönetici sıfırlama"; veritabanı sütunu ve yönetici ekranı gerektirdiği için V1-RMD-701'e ayrıldı.
- **B036/B077 (düşük yetkili veritabanı rolü, veritabanı TLS):** gerçek PostgreSQL ve TLS doğrulaması ile canlı yığına dokunmadan deneme gerektirdiğinden V1-RMD-700'e ayrıldı.
- **B117 izin kipi:** Windows'ta doğrulanamadı; yalnız kod yolu derlendi.
- **Ekran koruyucu "gövde okunmadı" kanıtı:** bozuk gövdeli yetkisiz yüklemenin 401 vermesiyle dolaylı; gövdenin hiç okunmadığını ölçen ayrı bir test yok.
- **Retention denetim olayı korelasyonu:** imha işinin kimlik içeren korelasyon kimliği için ayrı test yok (iş gerçek veritabanı ister); koşucu tarafı test edildi.
- **B080:** ana makine adı yerel adrese çözülüyorsa yakalanmaz (DNS yeniden bağlama).
- Hub taraması oturumun kayan bitişini ve `last_seen_at` değerini bağlı bağlantı için tazeler; bu değerleri okuyan boşta kalma kuralı yok.
