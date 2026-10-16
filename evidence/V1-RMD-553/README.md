# V1-RMD-553 - vanilla clients reporting

Ne yapıldı: `telemetry/telemetry.js` (Cashier ve WaiterPwa için birebir aynı kopya) yakalanmamış hata, reddedilen promise, başarısız `/api/` çağrısı ve tıklamaları raporlar. Tıklama anahtarı sırası: `data-telemetry-id` → `id:<id>` → `refund-action:<değer>`/`receipt-action:<değer>`/`action:<değer>` → `auto:<etiket>@<rota>`. Metin, mesaj, yığın ve girilen değer gönderilmez.
Kasiyer ana sayfası, 3 ödeme sayfası ve garson ana sayfasına betik eklendi; garson `sw.js` önbelleğine eklendi, `CACHE_NAME` v20 → v21.

Kabul koşulları (tests/Clients/StaticApps/telemetry-reporting.test.js, 15 test; tüm StaticApps 101/101, bkz. test-runs.log):
1. Ödeme ekranının 5 düğmesi (approve-confirmation, submit-tender, apply-split, print-receipt, refund-action request) sabit anahtarla kaydedilir.
2. Garson çevrimdışı kuyruğu: sığmayan depoda telemetri yalnız kendi anahtarını (`alkaros.telemetry.interactions.v1`, ≤20000 karakter) siler/keser; `alkaros_waiter_offline_queue` değişmez ve yazma kota hatası vermez.
3. Bu klasör.

Mutasyonlar: mutations.log (4 öldürüldü; 2 denk/gözlemlenemez, nedenleri orada).

Doğrulanamadı: gerçek tarayıcıda/cihazda davranış; garson oturumunun telemetri uçlarının beklediği çerezle aynı çerezi taşıyıp taşımadığı (sunucu tarafında 204 mutlu yol HTTP üzerinden test edilmedi).
