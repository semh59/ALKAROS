# V1-RMD-718 - Garson çevrimdışı kuyruğu ve hatalı tur ekranı

**Sonuç:** B001 kapandı: kuyruk 409'u artık sunucunun hata koduna göre ayırıyor. Yalnız `CONCURRENCY_CONFLICT` bekler; stok yetersiz, tablo hesabı açık, mükerrer kayıt, anahtar yeniden kullanımı gibi kalıcı 409'lar hatalı tur listesine gider ve arkasındaki turlar gönderilmeye devam eder. B028'in şu parçaları kapandı: hatalı tur Sil iki dokunuşlu (ilk dokunuş sorar), "Yeniden gönder" eklendi, bekleyen QR siparişi Reddet iki dokunuşlu, kuyruk düğmesi dokunma hedefi 28px → `--target-min`.

Yapılmayanlar (dürüst): hatalı turu sepete geri alıp düzenleme yok (yalnız aynı turu yeniden gönderme); Reddet'te gerekçe seçimi yok (sabit "Garson reddetti" kalır, yalnız onay eklendi); fiyat güncelle düğmesi 30px dokunma hedefi ve "İptal iste" ifadesi/rozet bu görevde doğrulanamadı, değiştirilmedi.

## Kanıt
- İstemci testleri: `logs/vitest-static.log` (tests/Clients/StaticApps tamamı, exit 0).
- Yeni/değişen testler: `tests/Clients/StaticApps/offline-queue.test.js` (kod bazlı 409 ayrımı; stok 409'u sonraki turu tutmaz; CONCURRENCY_CONFLICT kuyruğu korur ve durur), `failed-orders.test.js` (iki dokunuşlu silme, yeniden gönder, iki dokunuşlu ret).
- Mutasyon: `mutate.mjs`, `mutations.log` — 7/7 KILLED, kaynak her seferinde geri yüklenip karşılaştırıldı.
