# V1-RMD-570 - Performans bütçesi uç noktaları

Yeni uç noktalar (`/api/v1/management/observability/performance-budgets`):
- `GET /` bütçe listesi + varsayılan istek bütçesi (rapor görüntüleme yetkisi).
- `PUT /` bütçeyi kaydeder/günceller, `DELETE /?metric=&name=` siler (`observability.manage` yetkisi). Her değişiklik denetim kaydı bırakır (`monitoring.performance_budget.set` / `.removed`).
- Doğrulama: ölçüm türü bilinen 4 türden biri, ad 1-200 karakter, değer 0'dan büyük ve en çok 3.600.000 ms; aksi 400, kayıt yazılmaz.
- Kayıt `Monitoring:Enabled` anahtarına bağlıdır (kapalıyken uç noktalar yoktur).

Kanıt
- `test-runs.log`: gerçek Postgres, tam HTTP; 8/8 (yönetici kaydet/güncelle/sil + denetim sayısı; yalnız-görüntüleme yetkisi okur ama PUT/DELETE 403, oturumsuz 401, kayıt ve denetim izi yok; 6 geçersiz giriş 400).
- `mutations.log`: 7/7 mutant öldürüldü.
- `arch-run.log`: API sözleşme testi 5/5 (PUT/DELETE etkisiz tekrar olarak izin listesine yazıldı); `host-regression.log`: Host testleri 26/26.

Sınırlar (doğrulanamadı)
- Docker yığınında çalıştırılmadı; kanıt süreç içi HTTP testidir.
- Değer değişince değerlendirici bir sonraki dakikalık turda yeni bütçeyi okur (her turda tabloyu okuduğu koddan çıkarıldı, ayrıca ölçülmedi).
- Ekran yok; V1-RMD-579.
