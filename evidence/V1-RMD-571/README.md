# V1-RMD-571 - İzleme anahtarı ve olay fırtınası testi

Ne yapıldı
- `Monitoring:Enabled` (ortam değişkeni `Monitoring__Enabled`): varsayılan açık; yalnız açıkça `false` ise kapanır, boş ya da anlamsız değer izlemeyi açık bırakır. Kapalıyken izleme servisleri (olay yazıcı, dedektörler, izleme bağıntısı, dış çağrı ve sorgu planı dinleyicileri), izleme ara katmanları ve izleme uçları (tarayıcı hatası, kuyruk, etkileşim, hata listesi, yolculuk) hiç eklenmez; sunucunun geri kalanı çalışır (yanıtlar `X-Correlation-Id` taşımaz, izleme uçları 404 verir).
- Giriş ucunun güvenlik dedektörü isteğe bağlı oldu (izleme kapalıyken sunucu açılışı bu yüzden kırılıyordu).
- Fırtına testi: gerçek Postgres'te 8 işçili ana havuz iş yükü, yalnız iş yükü (taban) ve iş yükü + saniyede 1000 olay (fırtına) evrelerinde `pg_stat_activity` her 100 ms örneklenir; sonuç raporlanır.

Kabul koşulları
1. Ayar yoksa açık, kapalıyken yok: `SettingIsOnUnlessItIsExplicitlyFalse`, `MonitoringIsOnByDefault...`, `SwitchedOffNoMonitoringService...` (gerçek Host).
2. Fırtınada ana havuz bağlantısı değişmez: 5 sn'lik koşuda ana havuz en çok 8 bağlantı (taban 8), izleme havuzu en çok 1, 5012 olayın 5012'si kuyruğa alındı, düşen 0, başarısız yazım 0.
3. Bu klasör; mutasyon 5/5 yakalandı.

Bilinen sınırlar (doğrulanamadı / yapılmadı)
- Bu görevdeki fırtına 5 sn sürdü ve tek makinede (yerel Docker'daki Postgres) koştu; 30 sn'lik koşu ve Docker yığınındaki sayılar V1-RMD-556'da.
- Fırtına olayları 200 farklı anahtarda dolaştı; her olayın benzersiz olduğu (en kötü) durum denenmedi.
- Kapalıyken sunucu sağlık/uyarı gibi eski gözlem uçları (`MapObservabilityApi`) açık kalır; yalnız yeni izleme sistemi kapanır.
- İstemciler (tarayıcı) kapalıyken izleme uçlarına 404 alır ve sessizce geçer; bu davranış gerçek tarayıcıda denenmedi.
