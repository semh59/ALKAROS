# V1-RMD-823 sonuç

Kapatılanlar: PosTerminal `index.html` ve `nfc.html` Google Fonts isteği kalktı (yazı tipi yığını sistem yazı tipine düşer; depoda lisanslı yerel yazı tipi dosyası ve yeni bağımlılık izni olmadığından yerel paketleme yapılmadı); mutfak, yönetim (kapanış, hata olayları, güvenlik/sistem, kullanıcı yolculukları, stok, menü, masa QR), katalog fiyat tarihleri, röle/Token/QNB ayar ekranları ve online ekranların saatleri `Europe/Istanbul` biçimleyicilerine geçti (`formatShortDateTime`, `formatDate`, `formatDayMonthTime`, `venueToday`); eşit bölüştürmede kişi sayısı en çok 50 (alan `max=50`, hesap kırpar, alan kırpılmış değeri gösterir).

## Doğrulananlar (değişiklik gerekmedi)
- `cashier-app.js` kod eşleşmesi iki tarafı aynı `toLowerCase` ile karşılaştırır; Türkçe yerel ayarlı küçültme "SIR"/"sir" eşleşmesini bozardı.
- `ApiError.message` sunucunun Türkçe eşlenmiş metnidir (V1-RMD-114 denetimi); ham istemci hatası ekrana basılmaz.
- `Invalid Date`: `toLocale*String` geçersiz tarihte atmaz ("Invalid Date" yazar), önceki davranışla aynı.

## dist ve Docker (PO 10)
- `src/Clients/PosTerminal/dist/` ve `dist-nfc/` `.gitignore` (PosTerminal/.gitignore) ve `.dockerignore` içindedir; depoda izlenmez.
- Docker imajı `deploy/docker/Dockerfile` `frontend-build` aşamasında `corepack prepare pnpm@11.19.0` ile pnpm kurup `pnpm build` ve `pnpm build:nfc-standalone` çalıştırır; imaj dist'i kendisi üretir, elle dist taşımak gerekmez.
- Yerelde pnpm PATH'te yoktur; `node_modules/.bin/vite build` çalışır (bu görevde doğrulandı, dist'te `googleapis` 0). E2E bu yerel dist'i kullanır; eski bir `dist-nfc` yeniden derlenmedikçe eski Google Fonts bağlantısını taşıyabilir (izlenmeyen çıktı).

## Doğrulama
- PosTerminal vitest 995/995, tsc 0, oxlint 0 hata (2 eski uyarı); StaticApps 303/304, kalan 1 kırmızı bu görevin dışında (G5).
- Playwright E2E 04, 09, 12, 13, 22 yeşil (13/13).
- Mutasyon: 6 mutant (iki biçimleyici, 50 sınırı, iki HTML, bir CSS @import), 6/6 öldürüldü, cmp ile geri alındı.

## Bu görevde YAPILMAYANLAR (raporda açıkça açık)
Kalem "Kaldır" geri alma (T-20); indirim/bahşiş geri alma ve sunucu ucu (K18: G5'in `BillingSplitApplication.cs` değişiklikleri bitmeden dokunulamaz, geri alma için ek defter/ onay tasarımı gerekir); faturaId metni ve "Bekleyen hesaplara dön" düğmesi (T-11/K15); mikro metin ve kontrast (T-15/T-19); başlık tutarlılığı (T-23/T-24); T-21 etiketi; diğer F-kalemleri (F0905, F0390, F0354, F0324, F0401, F0432 ...). Ayrıca: WaiterPwa `index.html` hâlâ Google Fonts bağlantısı taşır (G5 alanı); tarih-yalnız iş günü alanları (`formatDay`, alış faturası tarihi) kasıtlı olarak dokunulmadı; mutfak "bugün" aralığı UTC günü kullanır (`KitchenOperationsWorkspace.tsx:221`), kanal/fatura/ürün marj raporları cihaz yerel gününü kullanır.

## Bağımsız incelemeler (qa-frontend, qa-test-kalitesi)
Giderilen: kalan saat dilimsiz `toLocale*String` yerleri (röle/Token/QNB ayarları, online ekranlar) biçimleyicilere geçti; harici istek testi CSS dosyalarını ve protokolsüz adresleri de tarar; gece yarısı sınırı (20:59:59Z, 21:00Z, yıl devri) ve kişi sayısı sınırları (51, 50, 1, boş) test edildi; kırpılan kişi sayısı alanda görünür; sahte saat `finally` ile geri alınır.
Kabul edilen: kapanış ekranında varsayılan gün için ayrı ekran testi yok (biçimleyici ve sınır testleri `venueToday`'i kapsar); kapanış/menü/stok `venueToday()` bileşen bağlanırken bir kez okunur (önceki davranışla aynı); `process.env.TZ` ataması test süreci boyunca UTC'de kalır (E2E/vitest diğer testleri bu dilimle de yeşil).
