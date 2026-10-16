# V1-RMD-556 - İzleme yükü ve fırtına ölçümü

Yöntem
- Sipariş onayı (submit-draft) p95: `load/order-confirm-ab.mjs`; her tur taze veritabanı (migrasyonlar + E2E tohum verisi) ve taze Host süreci; izleme `Monitoring__Enabled=true/false` ile dönüşümlü; 20 terminale yayılmış 300 sipariş (sunucunun terminal başına dakikada 120 yazma sınırı yüzünden), 30 ısınma siparişi atılır. Her turun p95 değeri kaydedildi, en iyi tur seçilmedi.
- Fırtına: V1-RMD-571'in `EventStormTests` testi 30 sn, saniyede 1000 olay, Docker'daki Postgres'e karşı; ana havuz iş yüküyle birlikte `pg_stat_activity` her 100 ms örneklendi.

Sonuçlar
1. Sipariş onayı p95: izleme açıkken medyan 58,8 ms, kapalıyken 57,8 ms; oran 1,017, sınır 1,10 (test-runs.log). İlk iki turda makine ısınmadığı için sayılar yüksek (103 / 86 ms); sonraki altı tur 57-59 ms.
2. Fırtınada ana havuz en çok 8 bağlantı (fırtınasız da 8), izleme havuzu en çok 1; 30012 olay kuyruğa alındı, düşen 0, başarısız yazım 0.

Kabul koşulu 3 (çıktılar bu klasörde) sağlandı. Tıklama ek yükü gerçek kasa cihazı ister; V1-RMD-572'ye taşındı ve cihaz olmadığı için beklemede.

Bilinen sınırlar (doğrulanamadı / yapılmadı)
- A/B ölçümü Docker kabı yerine aynı koddan derlenmiş Host sürecini (Debug derlemesi) çalıştırır; veritabanı Docker'daki Postgres. Docker `api` kabında A/B yapılmadı (kapı: kabın ortamı tek `Monitoring__Enabled` değeriyle yeniden başlatılıyor, ölçüm gürültüsü artar).
- Ölçüm tek makinede, tarayıcısız HTTP istemcisiyle; gerçek bir kasada ağ ve ekran süresi yok.
- Siparişler basit tek kalemli; modifier'lı veya çok kalemli sipariş denenmedi. Fırtına olayları 200 farklı anahtarda dolaşır.
- Bellek baskısı yüzünden bir koşu yarıda kesildi ve baştan yapıldı; sonuçlar yalnız tam koşudandır.
