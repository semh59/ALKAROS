# V1-RMD-810 - Müşteri ekranı: tek kaynaktan tutarlı döküm, anlık bildirim, tamamlanan ve iptal edilen hesap

- Task ID: V1-RMD-810
- Status: Done
- Assignee: claude-code-session_019vXeYXjE3sM6VZKpRMj1L9
- Work type: implementation
- Surface state: Existing

## Goal

Kasa incelemesinin G6 grubunun (müşteri ekranı) sunucu tarafı; ekran tarafı V1-RMD-812, eşleştirme V1-RMD-811. Kaynaklar: `7-birlesik-dogrulama.md` satır 14, 26, 30; `5-iletisim-agi.md` B5, B8; `3-tasarim.md` T-14, T-27; `1-kasiyer.md` O1/O3; `9-1020-kasa-eslesme.md` F0413 (`evidence/V1-RMD-739/findings/low-A.md:70`, "Ara toplam" etiketi `CustomerDisplay.tsx:360`). Kodda doğrulanan kusurlar:

1. Döküm tek kaynaktan değil. Ekran verisi (`DualScreenStore.Display.cs:219-314`) satır tutarını indirim sonrası `gross_amount` (ikramda kalem fiyatı), Ara toplamı indirim öncesi toplamı, büyük rakamı ise hesaptan alır; hesap indirimi ve bahşiş hiç görünmez. Sonuç: indirimli kalemde satır toplamı Ara toplama, ikramlı kalemde Toplama varmaz; hesap indirimi/bahşişi sonrası ekrandaki rakamlar birbirini tutmaz. Düzeltme: döküm şu eşitliği her durumda sağlar: satır tutarları (indirim öncesi, KDV dahil) toplamı = `Subtotal`; `Subtotal − DiscountTotal + TipTotal = Total = PaidTotal + RemainingTotal` (hesap varken). Hesap açılmışsa tüm rakamlar (satırlar dahil) hesabın kendi kalemlerinden, hesap indirimi ve bahşişinden, onaylı ödeme dağılımından gelir (`CustomerDisplayPaymentProgress`); hesap yoksa sipariş kalemlerinden gelir ve `Subtotal`/`DiscountTotal`/`Total` kalemlerden türetilir. Satır, kendi indirimini (`DiscountAmount`) taşır; ekran bununla "ilk fiyat, indirim, ikram" gösterebilir.
2. `/tip` ve `/discount` ekrana anlık haber vermiyor (`BillingSplitApplication.cs:228` yalnız hesap kapanırsa, `:250` hiç). Her ikisi de `DualScreenApplication` içindeki tek en-iyi-çaba bildirim yardımcısıyla `SnapshotChanged` gönderir; `TryCloseBillAsync` de aynı yardımcıyı kullanır.
3. Tamamlanan siparişte ödenen 0 ve toplamlar siliniyor (`CustomerDisplayPaymentProgress.cs:22,41-42`): ödeme tamamlanınca (`Completed`) ödenen ve toplam, kapanmış hesaptan doğru gelir.
4. Hesap iptal edilince ekran "Siparişiniz alındı" kalıyor (sipariş kapanmaz): siparişin tüm hesapları iptalse ve sipariş düzenlenmiyorsa (Draft değil) anlık görüntü `Idle` olur, satır ve toplam sızmaz; yeni hesap açılınca normal gösterime döner. Hesap iptal ucunun hub bildirimi ve siparişin kendisini iptal etmesi G5 görevinin işidir; bağlantı noktası: `DualScreenApplication.NotifyCustomerDisplayAsync` (bu görevde eklenir); `GetSnapshotAsync` iptal edilen veya reddedilen siparişi zaten `Idle` yapar. Bağlantı `evidence/V1-RMD-810/RESULT.md` içinde yazılır.

## Owned surface

- `tests/Host/MigrationComposition/DualScreen/CustomerDisplayStatementTests.cs`
- `evidence/V1-RMD-810/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Host/DualScreen/CustomerDisplayPaymentProgress.cs (V1-RMD-785) — hesaptan tek kaynaklı döküm, tamamlanan ve iptal edilen hesap.
  - src/Host/DualScreen/DualScreenStore.Display.cs (V1-RMD-097) — hesapsız siparişte satır indirimi ve türetilmiş toplamlar.
  - src/Host/DualScreen/DualScreenContracts.cs (V1-RMD-148) — satır indirimi ve bahşiş alanı.
  - src/Host/DualScreen/DualScreenApplication.Payments.cs (V13-PUI-001) — ortak bildirim yardımcısı.
  - src/Host/DualScreen/DualScreenApplication.Endpoints.cs (V1-IAM-024) — iki ekran verisi adresinde yeni bağımlılık.
  - src/Host/Experience/Billing/BillingSplitApplication.cs (V1-RMD-103) — indirim ve bahşişten sonra bildirim.
  - tests/Host/Experience/Billing/BillingSplitHttpTests.cs (V1-RMD-103) — indirim ve bahşişten sonra bildirim testi.
  - tests/Host/MigrationComposition/DualScreen/CustomerDisplayContractTests.cs (V1-RMD-008) — sözleşme izin listesi.
  - tests/Host/MigrationComposition/DualScreen/CustomerDisplayPaymentProgressTests.cs (V1-RMD-785) — yeni dökümün birim testleri.
  - tests/Host/MigrationComposition/DualScreen/DualScreenStoreTests.cs (V1-RMD-090) — satır tutarı ve indirim beklentileri.
  - tests/Host/MigrationComposition/DualScreen/CustomerDisplayBillBindingTests.cs (V1-RMD-785) — iptal hesap testi yeni dökümün birim testlerine taşınır.
  - tests/Host/Experience/PaymentTender/PaymentTenderHttpTests.cs (V13-PUI-001) — indirim, bahşiş, kısmi ödeme, tamamlanma ve iptalde ekran verisi.
  - tests/Host/Experience/PaymentTender/BillClosureLoggingTests.cs (V13-PUI-001) — ortak bildirim yardımcısı testi.
  - src/Clients/PosTerminal/src/contracts.ts (V1-RMD-031) — ekran verisi tipi.
  - src/Clients/PosTerminal/src/stale.test.ts (V1-RMD-020) — tip alanları için örnek veri.
  - src/Clients/PosTerminal/src/routes/CustomerDisplay.test.tsx (V1-RMD-374) — tip alanları için örnek veri.
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. `CustomerDisplayLineDto.DiscountAmount`, `CustomerDisplaySnapshotDto.TipTotal`; sözleşme izin listesi güncellenir.
2. Hesapsız siparişte: satır tutarı = indirim öncesi (gross + indirim; ikramda kalem fiyatı ve indirim = kalem fiyatı); `Subtotal` ve `DiscountTotal` satırlardan, `Total` = `Subtotal − DiscountTotal`.
3. Hesap varken (iptal edilmemiş hesaplar): satırlar hesap kalemlerinden; `Subtotal` kalem toplamı; `DiscountTotal` kalem + hesap indirimi; `TipTotal`; `Total` ayarlanmış ödenecek tutar; `TaxTotal` ayarlanmış KDV; `PaidTotal`/`RemainingTotal` mevcut ödeme dağılımı kuralıyla.
4. `Completed` siparişte kapanmış hesaptan doğru toplam ve ödenen; `RemainingTotal` 0.
5. Siparişin hesapları yalnız iptalse ve sipariş Draft değilse `Idle`.
6. `/discount` ve `/tip` sonrası `PaymentChanged` bildirimi (en iyi çaba, hata ödemeyi etkilemez).
7. Her düzeltme için önce kırmızı test, sonra yeşil; mutasyon turu.

## Out of scope

- Arayüz ve okunurluk (V1-RMD-812); eşleştirme süresi ve 429 (V1-RMD-811); hesap iptal ucunun bildirimi ve sipariş iptali (G5).
- Şema/migration, kasiyer satış ekranının (`Cashier.tsx`) bahşiş satırı (V1-RMD-812).

## Dependencies

- V1-RMD-785

## Acceptance evidence

1. Kırmızı sonra yeşil: Host testleri gerçek Postgres'te (port 55432): dökümün her senaryoda `Subtotal − DiscountTotal + TipTotal = Total = PaidTotal + RemainingTotal` ve satır toplamı = `Subtotal` eşitliği (indirimsiz, kalem indirimli, ikramlı, hesap indirimli, bahşişli, kısmi ödenmiş, çok hesaplı), tamamlanan sipariş, yalnız iptal hesaplı sipariş, `/tip` ve `/discount` bildirimi.
2. ApiConventions ve ModuleBoundaries testleri yeşil; PosTerminal tsc temiz.
3. Mutasyon: eşitliği bozan geçerli mutantlar testleri kırar; kaynak `cmp` ile geri alınır.
4. Plan audit ve consistency audit 0 hata/0 uyarı; kapsam aracı exit 0.
5. İki ardışık bağımsız qa-* incelemesi, bulgular giderilir, düzeltme sonrası doğrulama turu.
6. Semih'in elle deneyeceği senaryo: kasada %10 indirim ve bahşiş girilmiş bir hesabı ödeme sayfasında aç; müşteri ekranı verisi satırları, indirimi, bahşişi ve kalanı toplamı tutacak şekilde verir; indirim/bahşiş girince 5 sn beklemeden güncellenir (görünüm V1-RMD-812).

## Handoff

- None
