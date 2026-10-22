# V1-RMD-688 — Tünel rota yalıtımı

Sonuç: tamam. Röle ağından gelen istek, tünelin ilettiği ziyaretçi adresi ne olursa olsun yalnız QR/NFC rotalarına erişir; `/hubs` ve diğer `/api` rotaları 404 döner.

- Kapatılan bulgular: B001, B030, B075, B136 (güvenlik başlıkları, tüm ret yanıtlarında ve genel QR sayfasında).
- Testler: `composition-tests.log` — 22/22 başarılı (12 yeni test `TunnelRouteIsolationTests`).
- Mutasyon: `mutations.log` — M1–M9 hepsi KILLED, her biri geri yüklendi (kaynak diff'i doğrulandı).
- Doğrulanamadı: cloudflared'ın gerçek başlık davranışı (yalnız simüle edildi).
- Ertelendi: B136'nın WebPush yorumları V1-RMD-691 kapsamında.
