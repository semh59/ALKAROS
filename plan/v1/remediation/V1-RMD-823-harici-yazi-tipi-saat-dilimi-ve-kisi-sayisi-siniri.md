# V1-RMD-823 - PosTerminal harici yazı tipi isteği, yönetim/mutfak ekranlarında saat dilimi, eşit bölüştürmede kişi sayısı sınırı

- Task ID: V1-RMD-823
- Status: InProgress
- Assignee: claude-code-session_019vXeYXjE3sM6VZKpRMj1L9
- Work type: implementation
- Surface state: Existing

## Goal

Kasa incelemesinin G7 grubunun üçüncü parçasından bu görevde kapatılan kalemler (`2-arayuz-kodu.md` K14, K17; `3-tasarim.md` T-16 yazı tipi; `7-birlesik-dogrulama.md` satır 28 devamı). PO kararları (2026-11-04): PosTerminal `index.html` Google Fonts dış isteği kalkar (lisans/yerel dosya yoksa sistem yazı tipi); saat biçimleyicilerinde saat dilimi; kişi sayısı sınırı. Kodda doğrulanan durum:

1. `PosTerminal/index.html` ve `nfc.html` her açılışta `fonts.googleapis.com` ve `fonts.gstatic.com` adreslerine istek atar: çevrimdışı kasada bekleme ve gizlilik sızıntısı. Yazı tipi yığınları (`--ds-font-sans`) zaten sistem yazı tiplerine düşer; yerel yazı tipi dosyası için depoda lisanslı dosya yoktur ve yeni bağımlılık eklenemez, bu yüzden bağlantılar kalkar ve sistem yazı tipi kullanılır. Test: iki HTML'de harici adres yoktur.
2. Mutfak, yönetim kapanış, hata olayları, güvenlik/sistem, kullanıcı yolculukları, stok atığı, masa QR ve katalog fiyat tarihleri cihaz saat dilimine göre biçimlenir; `format.ts` biçimleyicileri (V1-RMD-821) `Europe/Istanbul` kullanır. Düzeltme: ortak `formatShortDateTime`, `formatDate`, `venueToday` eklenir ve bu yerler onları kullanır (tarih-yalnız iş günü alanları yerel gece yarısı olarak ayrıştırıldığından dokunulmaz). Gün başlangıcı `venueToday()` ile alınır.
3. Eşit bölüştürmede kişi sayısı üst sınırsızdır (yazılan 1e6 kişiye bölünür). Düzeltme: en çok 50; alan `max=50` taşır ve hesap sınıra kırpar.

Doğrulanıp değişiklik gerekmediği anlaşılanlar: `cashier-app.js` kod eşleşmesi iki tarafı aynı `toLowerCase` ile karşılaştırır (Türkçe yerel ayarlı küçültme "SIR"/"sir" eşleşmesini bozardı, bu yüzden değiştirilmez); `ApiError.message` sunucunun Türkçe eşlenmiş metnidir (V1-RMD-114 denetimi), ham istemci hatası değildir.

Bu görevde yapılmayanlar (ayrı görev gerektirir, raporda açıkça listelenir): kalem "Kaldır" geri alma (T-20), indirim/bahşiş geri alma ve sunucu ucu (K18; G5'in `BillingSplitApplication.cs` değişiklikleri bitmeden dokunulamaz, geri alma için ek defter tasarımı gerekir), faturaId metni ve "Bekleyen hesaplara dön" düğmesi (T-11/K15), mikro metin/kontrast (T-15/T-19), başlık tutarlılığı (T-23/T-24), diğer F-kalemleri.

## Owned surface

- `evidence/V1-RMD-823/**`
- `src/Clients/PosTerminal/src/external-requests.test.ts`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/PosTerminal/index.html (V1-RMD-145) — Google Fonts bağlantıları kalkar.
  - src/Clients/PosTerminal/nfc.html (V1-RMD-141) — Google Fonts bağlantıları kalkar.
  - src/Clients/PosTerminal/src/design-system/tokens.css (V1-RMD-114) — kalkan bağlantıları anlatan yorum.
  - src/Clients/PosTerminal/src/format.ts (V1-RMD-784) — ortak saat dilimli biçimleyiciler.
  - src/Clients/PosTerminal/src/format.test.ts (V1-RMD-784) — yeni biçimleyici testleri.
  - src/Clients/PosTerminal/src/features/kitchen-operations/KitchenOperationsWorkspace.tsx (V1-RMD-567) — saat dilimi.
  - src/Clients/PosTerminal/src/features/kitchen-operations/models.ts (V1-RMD-220) — saat dilimi.
  - src/Clients/PosTerminal/src/features/management-closing/ClosingSection.tsx (V1-RMD-792) — saat dilimi ve gün.
  - src/Clients/PosTerminal/src/features/management-error-events/models.ts (V1-RMD-577) — saat dilimi.
  - src/Clients/PosTerminal/src/features/management-security-system/models.ts (V1-RMD-450) — saat dilimi.
  - src/Clients/PosTerminal/src/features/management-user-journeys/models.ts (V1-RMD-548) — saat dilimi.
  - src/Clients/PosTerminal/src/features/management-stock/StockSection.tsx (V1-RMD-567) — saat dilimi ve gün.
  - src/Clients/PosTerminal/src/features/management-menus-recipe-cost/MenusSection.tsx (V1-RMD-567) — gün.
  - src/Clients/PosTerminal/src/features/management-menus-recipe-cost/RecipeCostSection.tsx (V1-RMD-567) — gün.
  - src/Clients/PosTerminal/src/features/management-table-qr/TableQrSection.tsx (V1-RMD-779) — saat dilimi.
  - src/Clients/PosTerminal/src/features/catalog/CatalogWorkspace.tsx (V1-RMD-784) — fiyat tarihleri.
  - src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.js (V1-RMD-264) — kişi sayısı sınırı.
  - tests/Clients/StaticApps/split-payment-hierarchy.test.js (V1-RMD-109) — kişi sayısı testi.
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Her kabul koşulu için önce kırmızı test, sonra düzeltme, sonra yeşil; mutasyon turu (`cp` + `cmp` ile geri alınır).
2. İngilizce kod ve test adları; ekrana basılan her metin Türkçe.
3. Dokunulan ekranların mevcut testleri yeşil kalır; saat dilimi testleri `process.env.TZ = "UTC"` ile cihaz dilimini sabitler.
4. PosTerminal `dist` bakımı: `dist/` .gitignore ve .dockerignore'dadır; Docker imajı `frontend-build` aşamasında `corepack` ile pnpm kurup `pnpm build` çalıştırır; yerelde `node_modules/.bin/vite build` çalışır (pnpm PATH'te yok). Bulgu RESULT.md'ye yazılır.

## Out of scope

- Sunucu, veritabanı, geçiş; Cashier ve WaiterPwa'nın kendi yazı tipi bağlantıları (WaiterPwa G5 alanında; RESULT.md'de not edilir).

## Dependencies

- V1-RMD-824

## Acceptance evidence

1. StaticApps ve PosTerminal tam paket yeşil; oxlint ve tsc temiz; kırmızı sonra yeşil: iki HTML'de harici adres yok, yeni biçimleyiciler İstanbul saatiyle (TZ=UTC), eşit bölüştürme 50'yi aşmaz.
2. Mutasyon: biçimleyicide `timeZone` kalkınca, sınır kalkınca, HTML'ye adres dönünce test kırılır; `cmp` ile geri alınır.
3. Plan audit ve consistency audit 0 hata/0 uyarı; kapsam aracı exit 0.
4. İki ardışık bağımsız qa-* incelemesi, bulgular giderilir.
5. Semih'in elle deneyeceği senaryo: kasayı internetsiz aç; sayfa yazı tipi beklemeden hemen açılır. Yönetim hata olayları ve mutfak ekranındaki saatler İstanbul saatindedir. Eşit bölüştürmede 500 yazınca 50 kişiye bölünür.

## Handoff

- None
