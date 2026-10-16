# V1-RMD-577 - Hata kategorileri için Türkçe etiketler

`models.ts` kategori sözlüğüne, sunucu kodunun hata olayı olarak ürettiği 24 kategori eklendi (db.slow_query_plan, db.n_plus_one, api.server_error_response, backup.*, integration.*, printer.*, payment.*, kitchen.ticket_not_accepted, background.job_missed, browser.offline_queue_backlog, db.pool_saturation, db.statement).

Kanıt: `test-runs.log` (PosTerminal hata ekranı testleri 38/38, tsc temiz, oxlint çıkış 0); `mutations.log` (3 etiket tek tek silindi, 3/3 test kırıldı, dosya geri yüklendi ve karşılaştırıldı).

Sınırlar (doğrulanamadı)
- Kategori listesi, `src` altındaki sunucu kodunda geçen kategori dizelerinden çıkarıldı; çalışma anında dinamik üretilen bir kategori varsa "Bilinmiyor" görünmeye devam eder (bu davranış testle korunuyor).
- Ekran görüntüsü alınmadı; etiketler bileşen testiyle doğrulandı.
