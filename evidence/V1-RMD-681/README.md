# V1-RMD-681 — ServerEndToEnd testi yeşil

Sonuç: `RealClientErrorProducesNoEvent` yeşil; proje 2/2 geçti. Kök neden üründe değil, testin veritabanındaydı.

Kök neden: Test veritabanı yalnız 187 numaralı geçişi uyguluyordu. Günlük temizlik servisi açılışta sonradan eklenen tabloları (health_checks, system_health_snapshots, notification_*, raw_ui_interactions) siliyor; tablolar yoktu, servis kendi `monitoring.retention_failed` olayını yazdı ve test onu "404 olay üretti" diye gördü. 404 yanıtı olay üretmiyordu.

Kanıt:
- `before-fix-red.log`: tabloda yalnız `monitoring.retention_failed|Background` kaydı vardı (hata ayıklama sorgusuyla).
- `test-e2e.log`: düzeltmeden sonra 2/2 geçti (028, 029, 187, 188, 191, 192 uygulanıyor).
- Mutasyon: eksik geçiş eklenmeden test kırmızıydı (`before-fix-red.log`).
