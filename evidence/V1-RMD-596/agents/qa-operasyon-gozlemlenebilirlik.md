# qa-operasyon-gozlemlenebilirlik (DAR) — V1-RMD-596 ham rapor özeti

Ajan commit: e03090d2 (baseline commit'i; baseline/head.txt = c2359719, aradaki fark yalnız evidence/baseline). Kontrol edilen kalem: 26. Çalıştırma yok, tüm bulgular Kod okuması.

| No | Önem | Kanıt | Özet |
|---|---|---|---|
| 1 | Yüksek | OutboxStore.cs:142, RetryPolicy.cs:154 | Tüketici istisnası loglanmıyor; last_error sabit "handler failure". Operatör ölü mektubun nedenini göremez. |
| 2 | Yüksek | OutboxDispatcherHostedService.cs:74,107; SecurityDetectors.cs:79 | Ölü mektup görünürlüğü yalnız toplamın artışına bağlı; biri requeue, biri ölürse net 0; watcher açılışta mevcut ölüler için alarm vermez; süreklilik alarmı yok. |
| 3 | Orta | OutboxStore.cs:272-274, 149 | Kira kaybı istisnası kalan claim'li mesajları 5 dk in_flight bırakır; log mesaj kimliği içermez. |
| 4 | Orta | OutboxAdministration.cs:33,63 | Requeue yalnız API; ekran ve operatör runbook'u yok. |
| 5 | Orta | 003-outbox-messages.up.sql; data-housekeeping.md | dispatched/dead satırlar için temizleme/saklama kuralı yok; sınırsız büyüme. |
| 6 | Orta | OutboxStore.cs:106-115 | Kira süresi dolunca pending'e dönerken attempt_count artmaz; süreci çökerten zehirli mesaj sonsuza dek döner, ölü mektup/alarm olmaz (çalıştırılmadı). |
| 7 | Düşük | OutboxAdministration.cs:90,117 | İki 404 durumu aynı metin; geri dönüş yolu yok. |
| 8 | Düşük | OutboxDispatcherHostedService.cs:39-40 | Log olay tipi/yönetim ucu vermez. |
| 9 | Düşük (doküman) | idempotency-inbox-outbox.md:24 vs OutboxComposition.cs:29-31 | Doküman "3 deneme" der; kod olay tipine özel bütçe kullanır. |

Temiz: kira/nesil eşleşmesi, SKIP LOCKED claim, tablo adı beyaz listesi, hata metni sanitize, requeue akışı (dryRun varsayılan, tek işlemde audit, testli), kuyruk derinliği metriği.
Doğrulanamayan: çökme döngüsü (6) ve toplu iş takılması (3) yeniden üretilmedi; dış bildirim kanalı bağlantısı; payload KVKK süresi.
Not: görev metnindeki OutboxAdministration yolu gerçekte src/Host/Experience/SecurityAdministration/OutboxAdministration.cs.
