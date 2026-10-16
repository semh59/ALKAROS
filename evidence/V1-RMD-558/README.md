# V1-RMD-558 - Sunucu dedektörleri ikinci parça

- `PoolUsageReader`: sürücünün havuz göstergelerinden (kullanılan/azami) uygulama havuzu doluluğu; izleme havuzu hariç. %85 ve üstü tek Uyarı, düşünce olay çözülür.
- `DbActivityTap`: sürücünün komut etkinliklerini dinler. Yavaş sorgu (>=500 ms Uyarı, >=2000 ms Hata), istek başına 20'den fazla sorgu (N+1, son 8 sorgunun şekli örnek). Sorgu metninden sabit değerler ve sayılar atılır.
- Öz kirlenme: izleme şeması (`observability.`) ve katalog (`pg_stat_*`, `pg_locks`, `pg_class`, `pg_settings`) sorguları yok sayılır.
- `ServerDetectorsDeepRunner` (15 sn): havuz, kaçan bakım işi (aralığın 2 katı, `MaintenanceJobRunner` durumundan), QR tüneli süreci (Running/NotConfigured dışı Hata); durum düzelince olaylar çözülür.
- Eşikler `Monitoring:ServerDetectorsDeep` bölümünden ayarlanır.

Kanıt: `test-runs.log` (9/9 gerçek Postgres testi, DualScreenHostTests 6/6), `mutations.log` (11 mutant, hepsi öldü).

Doğrulanamadı / yapılmadı:
- Sorgu planı (EXPLAIN): sürücü etkinliğindeki sorgu metni adlandırılmış parametre (`@ad`) içerdiğinden plan alınamıyor; V1-RMD-566'ya taşındı.
- İzleme havuzunun uygulama tablolarını okuyan sorguları (alan dedektörleri) metinle ayırt edilemez; yavaşlarsa yavaş-sorgu olarak görünebilir.
- Garson çevrimdışı kuyruk uzunluğu ve dış çağrı adresi temizleme V1-RMD-566'da.
- Gerçek kasa cihazında ve Docker yığınında yük/ek yük ölçülmedi (V1-RMD-556).
- Her istek için etkinlik oluşturulması küçük bir ek yük getirir; ölçülmedi.
