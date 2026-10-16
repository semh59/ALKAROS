# V1-RMD-540 - Hata yakalama sistemi Docker kanıtı

Ortam: yerel Docker yığını (`compose.yaml` + `compose.dev.yaml`, `http://localhost:8090`), güncel koddan derlenmiş `api`, `migrate`, `connector` ve `web` imajları (V1-RMD-573 ve V1-RMD-574 olmadan imajlar derlenmiyordu). Her enjeksiyon gerçek bir durumdur; sonuçlar `run.log` içinde, ekran görüntüleri `screen-*.png` dosyalarındadır. Betikler: `prove.mjs`, `printer-step.mjs` (olaylar), `screens.mjs` (Playwright ekran görüntüleri).

Kanıtlananlar (Docker çıktısı: run.log; ekran: screen-*.png)
| Enjeksiyon | Olay | Sayaç | Bildirim kuyruğu | Ekranda |
|---|---|---|---|---|
| Tarayıcı hatası (`js_error`, rota `/cashier/orders/42`) | `browser.js_error` Hata, rota şablonu `/cashier/orders/:n`, cihaz etiketi | Adet 2 (iki koşu) | Digest | "Ekran hatası", Tarayıcı |
| İstisna: `catalog.products` tablosu bir istek için yeniden adlandırıldı | `api.unhandled_exception` Kritik | 1 | Critical | "Beklenmeyen sunucu hatası", Kritik |
| Yavaş sorgu: `table_mgmt.tables` üzerinde ~2,6 sn kilit | `db.slow_query` Hata + `db.lock_wait` + `api.slow_request` + `db.slow_query_plan` ("Sort > Result > Seq Scan on tables") | slow_query 2-3 | Digest | "Yavaş istek", "Kilit bekleme" |
| Yazıcı kopması: kuyruk fişine bağlı Failed yazdırma işi | `printer.job_failed` Hata | 1 | Digest | Yazıcı, Açık; iş iptal edilince 70 sn içinde **Çözüldü** |
| Düğme etkileşimi + izleme kimliği | `raw_ui_interactions` kaydı, kullanıcı yolculuğunda "Düğme" adımı | Düğmeler sekmesinde `b.kanit540.tables` 1 kez, 55 ms | - | Zaman çizelgesi: Düğme → İstek → Veritabanı adımları |

Kabul koşulları
1. Her koşul için Docker çıktısı (run.log) ve ekran görüntüsü (screen-0..4) bu klasörde.
2. Elle deneme senaryoları aşağıda.
3. Bu klasör.

Semih'in elle deneyebileceği senaryolar (yerel yığın, `http://localhost:8090`, Yönetim > Hatalar ve Kullanıcı yolculuğu)
1. Tarayıcı hatası: kasada bir ekran hatası oluştur (ör. geliştirici konsolunda `throw new Error('x')`); Hatalar listesinde "Ekran hatası" görünür, adet artar.
2. Yavaş sorgu: veritabanında bir tabloyu birkaç saniye kilitle (`docker exec alkaros-postgres-1 psql -U alkaros -d alkaros`, ardından `begin; lock table table_mgmt.tables in access exclusive mode; select pg_sleep(3); commit;`), aynı anda Masalar ekranını aç; "Yavaş istek" ve "Kilit bekleme" görünür.
3. Yazıcı: `kitchen.print_jobs` içinde bir işi `Failed` yap; en geç 60 sn içinde "Yazıcı / Hata" görünür, işi `Cancelled` yapınca Çözüldü olur.
4. Düğme yolculuğu: kasada bir düğmeye bas, 10 sn bekle; Kullanıcı yolculuğu > Düğmeler'de anahtarı, İstekler'de zaman çizelgesini gör.
5. Telegram: bildirim kuyruğa düşer ama gönderilmez ("Telegram bildirimi yapılandırılmamış" yazısı); gönderim için çalışma ortamına bot anahtarı ve sohbet kimliği verilmesi gerekir (V1-RMD-557).

Bulgular (bu koşuda ortaya çıkan gerçek kusurlar; ayrı görev olarak açılacak)
- **Endpoint'in yuttuğu 5xx rapor edilmiyor:** Masa yönetimi uçları veritabanı hatasını kendisi yakalayıp 503 dönüyor (`table_mgmt.zones` yeniden adlandırılınca); paylaşılan hata işleyicisi hiç görmediği için olay üretilmedi (`S3b`). Aynı hata katalog ucunda olay üretti. Cevap kodu 5xx olan her yanıtın izlenmesi gerekir.
- **Docker imajında saat dilimi verisi yok:** `Europe/Istanbul` bulunamadığı için `OnlineOrderInvoiceDrafting` tür başlatıcısı patlıyor (api günlüğünde 9 kez `TimeZoneNotFoundException`). Online sipariş fatura taslağı kapsayıcıda çalışmaz.
- **Ekranda "Bilinmiyor":** `db.slow_query_plan` ve `db.n_plus_one` gibi kategoriler Türkçe çeviri sözlüğünde yok.
- **Sorgu planı kendi isteğinin zaman çizelgesine giriyor:** planlayıcının `EXPLAIN` sorgusu isteğin veritabanı adımları arasında görünüyor; izleme sistemine ait sorgu süzgecinden geçmiyor.
- **İstisna olayının adresi "unmatched":** `PostgresException on unmatched` yazıyor; rota bilgisi işleyiciye ulaşmıyor.
- Demo ürünlerin stoğu tanımlı değil (`PRODUCT_STOCK_NOT_CONFIGURED`), bu yüzden gerçek bir sipariş mutfağa gönderilemedi; yazıcı kanıtı için fiş, gerçek taslak siparişe doğrudan eklendi.

Bilinen sınırlar (doğrulanamadı / yapılmadı)
- Telegram gerçek gönderimi yok (kanal yapılandırılmamış); yalnız kuyruğa düşme kanıtlandı.
- Yazıcı enjeksiyonu: gerçek bir yazıcı kesilmedi, kuyruk satırı `Failed` yapıldı; fiş, `Draft` durumundaki bir siparişe bağlı.
- Test hesabı (`kanit.540`, yönetici rolü) ve kanıt satırları (taslak sipariş, fiş, yazıcı, yazdırma işi) geliştirme veritabanında kaldı; hesap kapatıldı (pasif). Olay satırları da duruyor, ekranda "Açık" görünebilir.
- Tarayıcı hatası ve düğme tıklaması gerçek tarayıcıdan değil, aynı uçlara HTTP ile gönderildi; istemci betiğinin kendisi (telemetry.js) bu koşuda çalıştırılmadı.
- Ekran görüntüleri Playwright ile, test hesabıyla alındı (Claude Chrome eklentisi bağlı değildi).
- Olay sayıları, betiğin ilk yarım kalan koşusu nedeniyle bazı kategorilerde 2 ya da 3 görünür.
