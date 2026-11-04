# V1-RMD-821 - Satış ekranında ilk dokunuşta sipariş, bekleyen hesapların okunur etiketi ve oturum hatası, saat dilimi, cari hesap ekranı doğrulamaları

- Task ID: V1-RMD-821
- Status: Done
- Assignee: claude-code-session_019vXeYXjE3sM6VZKpRMj1L9
- Work type: implementation
- Surface state: Existing

## Goal

Kasa incelemesinin G7 grubunun ikinci parçası (`7-birlesik-dogrulama.md` satır 27, 28, 29; `2-arayuz-kodu.md` K10, K14; `3-tasarim.md` T-18, T-21, T-22, T-26 (bekleyen hesaplar kısmı); `9-1020-kasa-eslesme.md` T10 içinden F0067). PO kararları (2026-11-04): ürüne ilk dokunuşta sipariş kendiliğinden açılır; bekleyen hesapta ana etiket masa/ad + saat; saat biçimleyicilerinde saat dilimi; cari hesapta tahsilat sonrası meşgul gösterimi, boş limit sıfır kaydetmesin, limit formu izne bağlı. Kodda doğrulanan kusurlar:

1. `Cashier.tsx`: ürün kartları sipariş açılana dek pasif ve her satışta "Yeni sipariş aç" / "Sipariş aç" / "Sonraki siparişi aç" ek dokunuşu var. Düzeltme: kartlar hep etkin; sipariş yoksa ya da gönderilmişse ürüne dokunmak yeni siparişi açar ve ilk ürünü ekler (tek eylem içinde, art arda iki dokunuş tek sipariş açar: işlemde olan istek ref'iyle korunur). Ürün seçenekli ise sipariş, seçim onaylanınca açılır (iptalde boş sipariş doğmaz). Sipariş açıldı ama ürün eklenemediyse açılan boş sipariş etkin sipariş olarak ekranda kalır ve sonraki dokunuş onu kullanır (başıboş sipariş bırakılmaz); sunucudaki boş sipariş temizliği ayrı görevdedir. Açma düğmeleri ve "Siparişe hazır" kartındaki düğme kalkar, kart "Ürüne dokununca sipariş açılır" der. Sipariş revizyonu açma yanıtından alınır.
2. `PendingChecksWorkspace.tsx` (T-21, T-22): satırın ana etiketi teknik sipariş numarası yerine "Masa 5 · 14:32" (masasız satışta "Hızlı satış · 14:32"), numara küçük ek bilgi; "Yanlışlıkla gönderildi" bağlantısı kartın içinde ikincil düğme olur.
3. `pendingChecksApi.ts` (F0067, T-26): 401 paylaşılan oturum sonlandırma işleyicisine gider (giriş ekranına döner); geri gönderme hatası sunucu metni yerine koda göre sabit Türkçe cümle (`TABLE_HAS_OPEN_CHECK`, `CHECK_HAS_PAYMENT`, `CHECK_NOT_RECALLABLE`, `INVALID_TABLE`).
4. Saat dilimi (K14, satır 28): `format.ts` `formatClockTime`, `PendingChecksWorkspace` saati, `BillSplitWorkspace` saati, `workspace.tsx` son güncelleme saati, `Cashier.tsx` eşleştirme bitiş tarihi, `cashier-app.js` bekletme saati, `customer-accounts.js` tarih biçimleyicisi `Europe/Istanbul` kullanır; kasa dışı ekranlardaki aynı eksikler V1-RMD-823'te.
5. `customer-accounts.js` (satır 29, K10, T-26): (a) "Tahsil et" sonrası meşgul durumu çizilir (düğme pasif); (b) kredi limiti alanı boşken kaydedilmez ("Kredi limitini yazın" doğrulaması, 0 yazılırsa 0 kaydedilir); (c) limit formu yalnız `settings.manage` izni olan oturumda gösterilir (sunucu ucu aynı izni ister); (d) sunucu metni her kodda basılmaz: kasa/cari uçlarının sabit cümleli kodları istemcide yazılıdır, alan doğrulama cümlesi (`VALIDATION_FAILED`), kredi reddi gerekçesi (`CREDIT_DENIED`) ve borç aşımı tutarı (`RECEIPT_EXCEEDS_BALANCE`) sunucunun Türkçe metniyle gelir, bilinmeyen kod duruma göre genel cümleye düşer.
6. Bu davranış `loginCashier` ve `startOrder` E2E yardımcılarını ve mevcut `Cashier.*` testlerini etkiler; güncellenir.

Kapsam dışı: kalem "Kaldır" geri alma, mikro metin/kontrast, başlık tutarlılığı ve diğer düşükler (V1-RMD-823); tahsilat sayfası yerleşimi (V1-RMD-822); dokunma hedefi ve axe (V1-RMD-824); sunucudaki boş sipariş temizliği (V1-RMD-802).

## Owned surface

- `evidence/V1-RMD-821/**`
- `src/Clients/PosTerminal/src/routes/Cashier.first-touch.test.tsx`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/PosTerminal/src/routes/Cashier.tsx (V1-RMD-097) — ilk dokunuşta sipariş, saat dilimi.
  - src/Clients/PosTerminal/src/routes/workspace.tsx (V1-CUI-006) — son güncelleme saati saat dilimi.
  - src/Clients/PosTerminal/src/routes/workspace.test.tsx (V1-CUI-006) — son güncelleme saati saat dilimi testi.
  - src/Clients/PosTerminal/src/strings.ts (V1-RMD-097) — yeni cümleler.
  - src/Clients/PosTerminal/src/styles.css (V1-RMD-042) — açma düğmesi stillerinin kalkması, boş durum kartı.
  - src/Clients/PosTerminal/src/format.ts (V1-RMD-097) — saat dilimli saat biçimleyicisi.
  - src/Clients/PosTerminal/src/format.test.ts (V1-RMD-784) — saat dilimi testi.
  - src/Clients/PosTerminal/src/features/pending-checks/PendingChecksWorkspace.tsx (V1-RMD-281) — etiket, ikincil düğme, saat dilimi.
  - src/Clients/PosTerminal/src/features/pending-checks/pendingChecksApi.ts (V1-RMD-281) — 401 ve sabit cümleler.
  - src/Clients/PosTerminal/src/features/pending-checks/pending-checks.css (V1-RMD-281) — kart yerleşimi.
  - src/Clients/PosTerminal/src/features/pending-checks/PendingChecksWorkspace.test.tsx (V1-RMD-281) — etiket, 401, geri gönderme testleri.
  - src/Clients/PosTerminal/src/features/billing/BillSplitWorkspace.tsx (V1-RMD-366) — saat dilimi.
  - src/Clients/PosTerminal/src/features/billing/BillSplitWorkspace.test.tsx (V1-RMD-366) — saat testi cihaz saatine değil anlık zamana bağlanır.
  - src/Clients/PosTerminal/src/routes/Cashier.sale-refresh.test.tsx (V1-RMD-719) — açma düğmesi yerine ürün dokunuşu.
  - src/Clients/PosTerminal/src/routes/Cashier.session-ended-message.test.tsx (V1-RMD-388) — açma düğmesi yerine ürün dokunuşu.
  - src/Clients/PosTerminal/src/routes/Cashier.modifier-picker.test.tsx (V1-RMD-774) — sipariş seçim onayında açılır.
  - src/Clients/Cashier/wwwroot/cashier-app.js (V1-RMD-109) — bekletme saati saat dilimi.
  - src/Clients/Cashier/wwwroot/payments/customer-accounts/customer-accounts.js (V1-RMD-443) — meşgul, limit doğrulaması, izin, hata cümleleri, tarih.
  - tests/Clients/StaticApps/customer-accounts-flows.test.js (V1-RMD-109) — yeni dosya: cari hesap akış testleri.
  - tests/E2E/Cashier/lib/testHelpers.js (V1-RMD-261) — giriş sonrası bekleme ve sipariş yardımcısı.
  - tests/E2E/Cashier/specs/04-complimentary-line.spec.js (V1-CUI-011) — açma düğmesi yok.
  - tests/E2E/Cashier/specs/30-customer-accounts.spec.js (V1-RMD-443) — limit formu izni.
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Her kabul koşulu için önce kırmızı test, sonra düzeltme, sonra yeşil; mutasyon turu (`cp` + `cmp` ile geri alınır).
2. İngilizce kod ve test adları; ekrana basılan her metin Türkçe; ham sunucu metni yalnız yukarıda sayılan Türkçe alan/gerekçe kodlarında.
3. Saat dilimi testi `TZ=UTC` ile koşar (makine İstanbul saatindeyse test boşa geçmesin).

## Out of scope

- Sunucu, veritabanı, geçiş; boş sipariş temizliği; yönetim, mutfak ve online ekranlarının saat dilimi.

## Dependencies

- V1-RMD-820

## Acceptance evidence

1. StaticApps ve PosTerminal vitest tam paket yeşil; oxlint ve tsc temiz; kırmızı sonra yeşil: kart pasif değil, ilk dokunuş siparişi açıp ürünü ekler, çift dokunuş tek sipariş açar, seçenekli üründe iptalde sipariş açılmaz, ekleme başarısızsa boş sipariş görünür kalır, bekleyen hesap etiketi, 401 girişe döner, sabit geri gönderme cümleleri, saat dilimi (TZ=UTC), cari hesapta meşgul/boş limit/izin/hata cümleleri.
2. Playwright E2E (04, 30 ve giriş yardımcısını kullanan özellikler) yeşil.
3. Mutasyon: her yeni davranış bozulunca ilgili test kırılır; `cmp` ile geri alınır.
4. Plan audit ve consistency audit 0 hata/0 uyarı; kapsam aracı exit 0.
5. İki ardışık bağımsız qa-* incelemesi (qa-frontend, qa-ux-tasarim), bulgular giderilir, düzeltme sonrası doğrulama turu.
6. Semih'in elle deneyeceği senaryo: kasada hiç sipariş yokken bir ürüne dokun; sipariş kendiliğinden açılır ve ürün sepette görünür. Bekleyen hesaplarda satır "Masa 5 · 14:32" gibi okunur. Cari hesapta kredi limitini silip Kaydet'e bas; kaydedilmez, uyarı çıkar.

## Handoff

- None
