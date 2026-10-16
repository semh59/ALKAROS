# V1-RMD-542 - Izleme kimliği zinciri

- İstemci: `installTracePropagation()` (`main.tsx`) aynı kökene giden `/api/` çağrılarına W3C `traceparent` ekler; izleme kimliği mevcut `X-Correlation-Id` (UUID) değerinin tireleri atılmış halidir, yoksa ikisi birlikte üretilir.
- Sunucu: `TraceCorrelationMiddleware` istek etkinliğinin izleme kimliğini `HttpContext.TraceIdentifier`'a yazar (denetim kaydı noktaları zaten `TraceIdentifier` kullandığı için değişmeden aynı kimliği taşır), günlük bağlamına koyar ve yanıtta `X-Correlation-Id` olarak döner. Hata olayları zaten `Activity.Current` izleme kimliğini taşıyordu.
- Alt aralıklar: `TraceSpanBuffer` yalnız açık bir isteğe ait veritabanı komutlarını (sorgu şekli, süre) ve dış çağrıları (süre; adres yok) tutar; istek başına en çok 50, fazlası sayılır; son 200 istek bellekte.

Kanıt: `test-runs.log` (sunucu 6/6 gerçek Postgres uçtan uca testi, PosTerminal 566/566, tsc 0, DualScreenHostTests 6/6), `mutations.log`.
Uçtan uca: tek `traceparent` ile giden istek; yanıt başlığı, `observability.error_occurrences.trace_id` ve `audit.audit_events.correlation_id` aynı değer.

Doğrulanamadı / sınırlar:
- Alt aralıklar kalıcı değildir (yalnız bellek); uygulama yeniden başlayınca silinir. Kalıcı zaman çizelgesi V1-RMD-548/541 kapsamına bağlıdır.
- Veritabanı komutları arka plan işlerinde (istek dışı) kaydedilmez.
- .NET 8 `HttpClient` etkinliği adres etiketi taşımadığından dış çağrı aralığı adsız ("external call") kaydedilir.
- Gerçek tarayıcıdan (Docker yığını) düğmeye basılarak denenmedi (V1-RMD-540).
- Denetim kaydı yazan her nokta `TraceIdentifier` kullanmaz (ör. yönetim hata ekranı onay kaydı kendi kimliğini üretir); değiştirilmedi.
