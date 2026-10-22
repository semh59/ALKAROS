# V1-RMD-690 — Müşteri ekranı: toparlanma, oturum, eşleştirme ve iptal deneyimi, sunucu testleri

Sonuç: tamam. Kapanan bulgular: B003, B004, B015, B017, B018, B019, B020, B027, B028, B066, B069, B070, B087, B114, B121, B126, B129; B074 kısmen.

- B004/B019/B020: ekran ilk yüklemede ağ, 5xx veya 429 hatasından sonra kendiliğinden yeniden dener (en çok 15 sn aralık). Eşleştirme yoklaması geçici hatada sürer, biten istekte (süresi doldu, kullanıldı, kilitlendi) yeni kod ister. Canlı bağlantı kapanırsa yeniden kurulur, geri gelince ekran yenilenir. Eski bir yanıt, yeni yanıttan sonra gelirse atılır.
- B015/B114: oturum kullanıldıkça uzar (12 saat kayan), satır dakikada en çok bir kez yazılır; çerez de uzar. Kasadaki eşleştirme penceresi bağlantının bitişini gösterir (`GET …/display-sessions/current`).
- B017: yeni ekran eşleşince aynı terminalin eski ekran oturumları iptal edilir.
- B018/B069/B126: eşleştirme hataları ayrı kod ve Türkçe mesajla gelir (yanlış kod, onaysız, süresi doldu, kullanılmış, kilitli); hata diyalog içinde görünür, kod kapatınca temizlenir. 5 yanlış sır sonrası tamamlama kilitlenir; `Location` başlığı artık istek kimliğini taşır.
- B066/B129: ekrandaki yönerge kasadaki gerçek düğme adını söyler; kod süresi gerçek süreden hesaplanır.
- B070: yetkiyi kaldırma onay ister ve kaç ekranın iptal edildiğini söyler.
- B087: kasa katalog sayfalarını son sayfaya kadar okur (önceden ilk sayfada kalıyordu).
- B121: sunucunun hiç üretmediği "Paying" durumu istemciden ve stilden silindi.
- B003/B027/B028: eşleştirme (mutlu, olumsuz, süresi dolmuş, kilitli), iptal, `display-sessions/current`, `orders/active`, kayan oturum ve `CustomerDisplayHub` bağlantı kapısı için sunucu testleri; istemci tarafında hata/yeniden bağlanma testleri.

Kanıt:
- `server-tests.log`: DualScreen sunucu testleri 43/43 (`DualScreenAuthorizationHttpTests` + `DualScreenScreensaverTests`). `host-tests-full.log`: tüm Host.Tests'te 3 test düşmüştü; kök neden `last_seen_at` boş olan oturumun yeni sorguda yetkisiz sayılmasıydı, düzeltildi ve bu üç test (screensaver) artık geçiyor; düzeltmeden sonra tüm Host.Tests yeniden koşulmadı.
- `pos-all-tests.log`: PosTerminal 730/730, `tsc --noEmit` temiz. `api-conventions.log` 5/5, `module-boundaries.log` 10/10, `composition-tests.log` 22/22.
- Mutasyon: `mutations.log`, 25 mutantın 25'i KILLED, her biri geri yüklendi (bellek baskısı S10'u yarıda kesti; kaynaktan elle geri alındı ve S10 yeniden koşuldu).
- Doğrulanamadı: gerçek çift monitörlü kasada uçtan uca deneme yapılmadı. B074'ün "yanlış onay kodu sayılmıyor" yarısı kalıyor: onay isteği kodla eşleşmediğinde hangi isteğe ait olduğu bilinmediği için sayaç tutulamaz; koruma hız sınırı (dakikada 10).
- Ertelendi: table-draft siparişlerinin müşteri ekranında görünmesi (S9) ve push hata yolu testleri (V1-RMD-691).
