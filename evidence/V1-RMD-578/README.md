# V1-RMD-578 - Zaman çizelgesinde EXPLAIN adımı ve istisna olayında rota adı

Değişiklikler
- `TraceCorrelation.cs`: `EXPLAIN` ile başlayan Npgsql komutları isteğin zaman çizelgesine adım olarak eklenmez (planlayıcının kendi sorgusu).
- `ServerDetectors.cs`: `api.unhandled_exception` olayı rotayı `IExceptionHandlerFeature.Endpoint` üzerinden alır; eskiden istisna işleyicisi uç noktayı temizlediği için "unmatched" yazıyordu.

Kanıt: `test-runs.log` (gerçek Postgres; ServerDetectors 10/10, TraceCorrelation 7/7), `mutations.log` (2/2 mutant öldürüldü, dosyalar geri yüklenip karşılaştırıldı).

Sınırlar (doğrulanamadı)
- Rota testi `IExceptionHandlerFeature`'ı elle kurar; ASP.NET'in gerçek istisna ara katmanının bu alanı doldurduğu Docker yığınında yeniden denenmedi.
- Yalnız `EXPLAIN` süzülür; izleme sisteminin diğer sorguları (olay yazımı vb.) için çizelgede adım görünüp görünmediği bu görevde incelenmedi.
