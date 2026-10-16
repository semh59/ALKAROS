# V1-RMD-566 - Kalan sunucu dedektör kalemleri

Ne yapıldı
- Çevrimdışı kuyruk: `POST /api/v1/telemetry/offline-queue` yalnız bir sayı alır; terminal etiketi oturumdan gelir. Sayı sınırın (20) altındaysa olay çözülür, sınıra ya da üstüne çıkınca olay açılır/yeniden açılır (terminal başına 1 olay, olay metninde sayı yok). Oturumsuz çağrı 401, geçersiz sayı (boş, negatif, 100000 üstü) 400.
- Dış çağrılar: `ExternalCallTap` giden HTTP çağrılarını süresiyle izler; 2 sn üstü yavaş (10 sn üstü Error), cevapsız veya 4xx/5xx başarısız olay olur. Adres olaya `sunucu/yol` biçiminde girer: sorgu, parça, kullanıcı bilgisi, port atılır; yoldaki yalnız düz kelimeler ve `v1` gibi sürümler kalır, gerisi `:id` olur (Telegram bot anahtarı gibi yol parçaları dahil).
- Yavaş sorgu planı: yalnız tek bir SELECT için, izleme havuzunda, 3 sn zaman sınırıyla, aynı anda en fazla bir tane, aynı sorgu şekli 10 dakikada bir. Sürücünün adlandırılmış parametreleri `$n`'ye çevrilir ve `EXPLAIN (GENERIC_PLAN)` ile (sorgu çalıştırılmaz) düğüm türleri ve tablo adları `db.slow_query_plan` olayına yazılır; maliyet, satır sayısı ve filtre metni yazılmaz.
- Ek: önceki görevlerin 4 POST ucu ile yeni uç `idempotency_allowlist.json`'a gerekçe ve görev ile eklendi; ApiConventions testi yeşil.

Kabul koşulları
1. Eşiği aşan bildirim 1 olay, eşik altı çözüm: `QueueOverTheLimitRaisesOneEventResolvesBelowItAndReopensWhenItGrowsAgain`, `EachTerminalHasItsOwnEvent...`.
2. Adreslerde sorgu/kimlik/anahtar yok: `AddressKeepsOnlyHostAndPlainWordSegments` ve `FailedAndSlowOutgoingCallsBecomeEvents...` (gerçek HttpClient, gerçek yerel sunucu; olay metninde SECRET/abc123/t0k3n yok), `CallThatGetsNoAnswer...`.
3. Bu klasör. Mutasyon: mutations.log (12/12 yakalandı).

Bilinen sınırlar (doğrulanamadı / yapılmadı)
- Geçerli oturumlu istek (204) HTTP üzerinden denenmedi; yalnız 401 ve ingest sınıfı gerçek veritabanıyla test edildi.
- Garson istemcisi henüz kuyruk uzunluğunu göndermiyor (istemci kodu bu görevin kapsamı dışında); uç hazır, gönderen yok.
- Eşik (20) ve dış çağrı süreleri `Monitoring:ExternalCalls` ile ayarlanır; kuyruk eşiği yapılandırmaya bağlanmadı.
- .NET 8'de HTTP etkinliği adres etiketi taşımadığı için dış çağrılar tanı dinleyicisinden okunur (yansıma ile). Mevcut izleme tamponundaki dış adım adı bu yüzden "dış çağrı" olarak kalır; bu görevde değiştirilmedi.
- Tür çıkarılamayan parametreli sorgularda plan alınamayabilir; bu sayılır (`Failures`) ve olay yazılmaz. Tek sorgu şekli türü belirsiz bir örnekte doğrulandı; her sorgu şekli denenmedi.
- Plan, gerçek bir tarayıcı/üretim yükü altında ölçülmedi.
