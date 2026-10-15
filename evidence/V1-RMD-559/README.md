# V1-RMD-559 kanıtı

Bağımsız denetimin (534, 552+538, 554+555+547) bulgularından düzeltilenler; her biri için test ve mutasyon.

## Düzeltilenler
- 552: tampon dosyası geçici dosya + taşıma ile atomik yazılır; bozuk satır atlanır ve sayılır; boşaltmada yalnız gönderilenler silinir (sonradan eklenenler ve kısmi başarıda kalanlar korunur, çift gönderim yok); zaman aşımı (TaskCanceled/Timeout) taşıma hatası sayılır; devre kesici kilitli.
- 538: ağ kesintisi (RetryLater) denemeleri tüketmez, bekleme üst sınırı 10 dk; yalnız Rejected 3 denemede Failed olur; aynı anda tek dağıtıcı (advisory lock); retry_after taşması/ayrıştırma hatası dağıtıcıyı düşürmez (1-3600 sn sınırı); terminal etiketi yalnız [A-Za-z0-9:_-] ve en çok 40 karakter.
- 534: gözlemci hata fırlatsa bile işleyici zinciri sürer; veritabanı hatası artık tek olay üretir (api olayı eklenmez); saklama işi hatası `monitoring.retention_failed` olayı olur.
- 554: yetki reddi sink'i Singleton kaydı (ValidateScopes uyumu); outbox izleyicisi hata sonrası son bilinen sayıyı korur ve hata olayı üretir.
- 555/547: arka plan döngüleri geniş hata yakalar, host'u düşürmez; hata sayacı tutulur.

## Test
CrashChannel 7, ErrorNotifications 15, ServerDetectors 6, SecurityDetectors 4, HealthSnapshot 7, ResourceProbes 5: hepsi geçti. Mutasyon 7/7 öldürüldü (`mutations.log`).

## Düzeltilmeyenler (bilinçli, denetim bulgusu açık)
- 534: gerçek HTTP isteğiyle uçtan uca gözlemci testi (WebApplicationFactory) yok; kayıt sırası testsiz.
- 552: sınıflar Host akışına hâlâ bağlı değil (V1-RMD-557); süreçler arası dosya kilidi yok (bekçi ayrı süreç olunca gerekir); bayt tabanlı sınır değil karakter tabanlı.
- 555: veritabanı kapalıyken "Unhealthy" kaydı yazılamaz (ekran son iyi kaydı gösterir); "yedek yaşı" yok.
- 538: HTTP istemci günlük bastırma Host bağlanırken yapılacak; Failed kayıtların yeniden kuyruğa alınması yok.
- 554: giriş terminal kimliği doğrulanmıyor ve sayaç global.
- 547: işlemci okunmuyor; disk uygulama dizininin diski.
