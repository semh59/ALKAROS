# V1-RMD-527 kanıtı

- Kanal raporu satırı `RefundedValue` taşır: siparişin hesabına bağlı ödemelerin ters kayıt toplamı, siparişin oluştuğu günün satırında.
- `AcceptedValue` ve diğer alanlar değişmedi (brüt).
- Test: `ChannelReportTests` 9/9 gerçek Postgres (yeni test: iki ödemeli kısmi iade + tam iade + iadesiz sipariş + başka gün siparişi; beklenen satır 85,50 iade). Çıktı `test-runs.log`.
- Mutasyon: 4 öldü, 1 eşdeğer (sorgu tarama sınırı) — `mutations.log`.
- Doğrulanmadı: kanal raporunu gösteren bir arayüz ekranı bu görevde değiştirilmedi; uç nokta JSON'una `refundedValue` alanı eklenir, ekranda gösterilip gösterilmediği kontrol edilmedi.
