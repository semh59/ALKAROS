# V1-RMD-464 kanıtı

- `module-tests.log`: `ALKAROS.Reconciliation.OnlineOrders.Tests` 34/34 geçti (7 yeni test: Accepted/Preparing/Ready eşik üstü vaka açar ve yeniden denenemez, eşik altı ve tamamlanmış sipariş vaka açmaz, iki platformda aynı numara iki vaka, teslimden sonra çözülür, atlanan vaka yeniden açılmaz).
- `reconciliation-host-tests.log`: Host uzlaştırma testleri 23/23 (tarama artık 10 kaynak çifti döndürüyor).
- `vitest.log`, `lint.log`, `typecheck.log`: Sorunlar sekmesi yeni Türkçe etiketlerle exit code 0.
- `mutation-completed-included.log`: sorguya `Completed` durumunu ekleyen mutasyon 2 testi kırdı; kaynak geri alınıp orijinalle aynı olduğu doğrulandı.
- `gercek-deneme.live.log` + `gercek-deneme.png`: gerçek Host + veritabanı + tarayıcı. 4 saatlik Accepted online sipariş kaydedildi, zamanlanmış tarama (elle çağrı yok) `NotHandedOver` vakası açtı; Sorunlar sekmesinde "Sipariş saatlerdir teslim edilmedi ya da iptal edilmedi", önerilen eylem Türkçe, "Yeniden dene" düğmesi yok.
