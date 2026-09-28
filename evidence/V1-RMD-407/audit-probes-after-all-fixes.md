# V1-RMD-399 probe'ları — V1-RMD-400..407 düzeltmelerinden sonra

Koşu: `audit-probes-after-all-fixes.log` (V1-RMD-407 değişikliği dahil çalışma ağacı, gerçek PostgreSQL 18).
Sonuç: 25 probe'dan 24'ü geçti. Kör koşuda (V1-RMD-399) başarısız olan 9 probe'dan 8'i artık geçiyor:
A3 (H-01), C3 sahipsiz hesap (H-07), C4 (H-04), C6 (H-03), C7 kendi hesabı (H-06), D2 (H-02, Q-01), D3 garson ve
mutfak personeli (H-02).

Kalan tek kırmızı `C7OfflineReconciliationUsesTheCallersRealRole`, düzeltmenin probe'un öngördüğünden daha katı olmasından
kaynaklanır: probe sahte rolle gönderilen eylemin istek içinde `Denied` satırı almasını bekliyordu ve yardımcısı 2xx
şartı koyuyordu; V1-RMD-404'ten sonra sunucu, çağıranın sahip olmadığı bir rolü içeren isteği bütünüyle 403
`IDENTITY_MISMATCH` ile reddediyor (log'daki hata gövdesi). Sınanan kural — sahte rol canlı politika kontrolünü
atlatamaz ve grant satırına yazılamaz — sağlanıyor; bu, V1-RMD-404'ün
`AClaimedRoleTheUserDoesNotHoldIsRefused` testiyle de doğrulanıyor.
