# V1-RMD-543 - Performans ölçümleri

- Tablo `observability.perf_minute_summaries` (migration 189): dakika, metrik, ad başına örnek sayısı, hata sayısı, toplam, en büyük değer ve 116 kovalı logaritmik histogram (her üst sınır bir öncekinin 1,1 katı).
- `PerformanceRecorder`: bellekte toplar, tamamlanan dakikaları 60 sn'de bir yazar (aynı dakika tekrar yazılırsa histogramlar toplanır), kapanışta kalanı yazar; yazma başarısız olursa örnekler tutulur ve sayılır. Dakikada en çok 600 satır: fazla adlar `other` altında toplanır, örnek kaybolmaz. 14 günden eski satırlar silinir.
- `PerformanceReader`: yüzdelikler (p50/p95/p99) aralıktaki dakikaların kovaları toplanarak hesaplanır; dakika yüzdeliklerinin ortalaması alınmaz.
- Ölçümler: `request` (yöntem + rota şablonu, 5xx hata), `db_command` (sorgu şekli; izleme şeması ve katalog sorguları hariç), `queue_depth` (outbox bekleyen+işlenen, dakikada 1 örnek), `ui_interaction` (sabit metrik adı hazır; besleme V1-RMD-541'de).
- Host kaydı: `PerformanceRecorder` + `ActivityPerformanceFeeder` + `PerformanceReader` (`DualScreenApplication.cs`).

Kanıt: `test-runs.log`, `mutations.log`. 10.000 örnekle (lognormal, düzgün, üstel) p50/p95/p99 hatası %5'in altında; 5.000 farklı ad tek dakikada en çok 600 satır; toplam örnek 5.000 korunur.

Doğrulanamadı / sınırlar:
- Hata oranı ölçümü yalnız istek (5xx) ve veritabanı komutu (etkinlik hata durumu) için; ekran etkileşimi hata oranı V1-RMD-541 ile gelecek.
- `queue_depth` yalnız outbox; yazdırma kuyruğu ve bildirim kuyruğu ayrıca örneklenmedi.
- Gerçek yük altında (Docker/kasa) ek yük ölçülmedi (V1-RMD-556).
- Tam `ALKAROS.Host.Tests` çalıştırması bu oturumda yalnız süzgeçli (Manifest + DualScreenHostTests 23/23) doğrulandı; tam çalıştırma sonucu aşağıdaki not satırındadır.
