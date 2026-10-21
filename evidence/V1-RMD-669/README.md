# V1-RMD-669 kanıtı

- B005: dört karar eylemi hata verirse Türkçe uyarı gösterilir, liste kalır; işlem sürerken düğmeler kapalı; yetki rotası eylemden sonra sessiz yeniler (ekran boşalmaz).
- B006: rol Türkçe, tarih/saat İstanbul saatiyle, 12 sebep kodunun hepsi Türkçe. Ad gösterimi bu görevde yok (sunucudan ad gelmesi V1-RMD-672).
- B029: pasifleştirme onay ister; kart aranan kullanıcı adıyla yenilenir; hesap açılınca rol listesi yenilenir; onay satırları 48 px.
- Testler: test-vitest.log (19 dosya, 124 test yeşil), typecheck.log (tsc 0).
- Mutasyon: mutations.log. E2 (`if (busy) return` koruması) hayatta kaldı: düğmeler zaten kapalı olduğundan eşdeğer, savunma amaçlı.
- Doğrulanamadı: tarayıcıda elle deneme yapılmadı.
