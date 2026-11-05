# V1-RMD-826 sonuç

Kapatılanlar: kalem "Kaldır" iki adım ister (Evet, kaldır / Vazgeç; odak güvenli seçeneğe gider, sipariş değişince soru kapanır, düzenlenemeyen siparişte pasif); mikro metinler en az 12 px ve kontrast >= 4,5:1 (CSS'ten okuyan test); sol menüde ayrı simgeler (bekleyen hesaplar saat, online gönder, yetki kilit, yönetim marka); dar ekranda (<= 600 px) ikincil başlık eylemleri "Menü" listesinde (bir eylem veya Esc ile kapanır, katalog bağlantısı karışmasın diye başlıkta "Ürün menüsü"); bölge seçiminde kat planı tek istek (F0905); Cashier.tsx'te `exhaustive-deps` uyarısı giderildi.
T-21 (bekleyen hesap etiketi masa/saat) V1-RMD-821'de kapandı (PendingChecksWorkspace testleri). T-23 başlık eylemleri zaten tek stil (`.header-action`); simge tekrarı bu görevde giderildi.

## Doğrulama
- PosTerminal vitest 1011/1011, tsc 0, oxlint 0 hata (kalan: giriş alanı `autoFocus` bilinçli, mutfak kullanılmayan işlev bu görev dışı).
- Playwright E2E 01-04, 13, 14, 20-23, 26, 27, 31 yeşil.
- Mutasyon 11 mutant (onay, vazgeç, dar ekran, kontrast, boyut, dokunma, simge, menü kapanma, odak, Esc), hepsi öldürüldü.
- dist/Docker (PO 10): `node_modules/.bin/vite build` yerelde çalışıyor (pnpm PATH'te yok); `dist/` .gitignore ve .dockerignore'da; Dockerfile `frontend-build` aşamasında corepack ile pnpm 11.19.0 kurup `pnpm build` çalıştırır.

## İncelemeler (qa-frontend, qa-ux-tasarim)
Giderilen: menü eylemden sonra/Esc ile kapanır ve geniş ekrana geçince sıfırlanır; "Menü" adı çakışması; aria-controls; odak; Evet/Vazgeç aralığı 16 px; sipariş değişince soru temizlenir; düzenlenemeyen siparişte Evet pasif; taşma koruması.
Kabul edilen: kaldırma geri alması yapılmadı (satır verisinde ürün/seçenek kimliği yok, onay seçildi); "send" ve "brand" simgeleri mevcut set içinde en yakın seçimler (yeni simge çizimi ayrı tasarım işi); bölge seçiminde tam ekran yükleme önceki davranışla aynı; eşik 600 px CSS kırılımından ayrı (tablet genişliğinde başlık sığıyor).
