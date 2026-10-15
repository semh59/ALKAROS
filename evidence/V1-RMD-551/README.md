# V1-RMD-551 kanıtı

- İzleme için ayrı küçük veritabanı havuzu: `MonitoringDataSource` (en çok 4 bağlantı, uygulama adı `alkaros-monitoring`), `AddMonitoringDataSource` ile ana sunucuda kayıtlı. Ana havuzdan bağımsızdır.
- Tutarlılık denetimi: `src/Host/Observability/**` alanı `observability` şemasına eşlendi (`HOST_AREA_SCHEMA`); denetim temiz.
- Test projesi `ALKAROS.Host.Observability.Wiring.Tests` çözüme ve proje manifestine kayıtlı (manifest doğrulayıcı: VALID).
- Test: 2/2 gerçek Postgres (5. bağlantı havuz tükendi hatası verir, uygulama adı sunucuda görünür, uygulama havuzu etkilenmez); mimari testler 9/9; mutasyon 3/3 öldü (`mutations.log`).
- Bu görevde yapılmayanlar (gereksiz çıktı): yönetim menü girişi — mevcut Sistem bölümü kullanılacak (V1-RMD-550 kararı); sentetik veri işareti şemada V1-RMD-533'te tanımlanır; izleme yazımlarının yavaş-sorgu dedektöründen ayıklanması V1-RMD-534'te.
- Doğrulanmadı: havuz sınırının canlı yük altında davranışı (V1-RMD-556).
