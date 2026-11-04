# V1-RMD-824 sonuç

Dokunma hedefleri: kasa hareketi gerekçe çipleri, kupür sayım girdisi, EFT onay kutusu (artık 48 px), vanilla kasa seçenek satırı, üst çubuktaki Kasa ve Cari bağlantıları ve React kabuğu arama kutusu en az 48 px. Tahsilat sayfasının kullanılmayan `.sp-tab(s)` stilleri kaldırıldı; yöntem seçimi `radiogroup/radio` olduğundan `role=tab` yön seçimi yoktur (kasa oturumu sayfasında `.cs-tab` kullanımı da yok, doğrulandı). Pasif düğmeler `opacity` ile değil belirteç renkleriyle çizilir (metin/arka plan >= 4,5:1). Eşit bölüştürme katlanır bölüme alındı (ödeme düğmesi yukarı çıktı).

## Doğrulama
- Yeni testler: `cashier-touch-targets.test.js` (7), `dynamic-a11y.test.ts` (axe 8 dinamik durum, kontrast: iki stil dosyasında 12 çift + opacity yasağı), `touchTargets.test.ts` arama kutusu; `split-payment-hierarchy` eşit bölüştürme testi.
- PosTerminal vitest 988/988, tsc 0, oxlint 0 hata (2 eski uyarı); StaticApps 301/303, kalan 2 kırmızı bu görevin dışında (G5).
- Playwright E2E (gerçek Host + Postgres): 05, 07-12, 14-16, 18, 21, 24, 25, 28-31 yeşil (60/60, ölçüm betiği dahil).
- Mutasyon: 9 mutant (çip 44 px, onay kutusu 32 px, oturum bağlantısı, arama kutusu, iki kontrast belirteci, axe kimlik/etiket, eşit bölüştürme bölümü), hepsi öldürüldü, cmp ile geri alındı (mutation-summary.txt).
- Gerçek Chromium ölçümü (touch-sweep.json, betik touch-sweep.spec.js.txt): 360/768/1280 px genişlikte EFT onay kutusu 48x48, yöntem çipleri >= 48, katlanır başlıklar 48, üst çubuk bağlantıları 48, yatay taşma 0; Kalan 28 px, Hesap toplamı 20 px.
- Ödeme düğmesi tahsilat kartında alta yapışkandır (sticky, kaydırma payıyla): 360/768/1280 px genişlikte 800 px yükseklikte alt kenarı y=792, yani her zaman ilk ekranda; E2E tıklamalarını örtmedi (60/60).

## Bağımsız incelemeler (qa-ux-tasarim, qa-test-kalitesi)
Giderilen: EFT onay kutusu etiketle ortalanır; "Ödemeyi Ekle" alta yapışkan (ilk ekran); katlanır başlıklar çerçeveli; pasif düğme belirteç renkleri kasa oturumu ve ana kasa düğmelerine de uygulandı; kontrast testi iki stil dosyasını ve beyaz-zemin çiftlerini kapsar; boyut regex'i `line-height`/`max-width` ile karışmaz ve seçicinin her geçişini tarar; `.sp-btn` ve yöntem çipi listede; durum testleri durumun çizildiğini de doğrular.
Kabul edilen: alert bant zeminlerinin yarı saydam kontrastı ve hayalet düğme pasif görünümü (belirteçlerle aynı çift, ayrı ölçüm gerektirmez); pasif yöntem çipi kesik kenarlığı ayrı dildir (kasıtlı: açıklama taşır).
