# V1-RMD-1091 - Bölmede her ödeme satırı ödeyen kişiyi söyler

- Task ID: V1-RMD-1091
- Status: Done
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

Semih kararı 2026-11-14: bölmede her ödeme satırında ödeyen kişi işareti (Kişi 1, Kişi 2) olsun. Sunucu sözleşmesinde ödeme satırında ödeyen alanı yoktur ve sözleşme değişmez; en küçük istemci tarafı çözüm uygulanır. Eşit bölme sürerken ödeme formu "Ödeyen: Kişi k / N" der. Kayıtlı dağılımdan bir kişinin payına dokunulmuşsa o kişinin gerçek adı kullanılır. Ödeme başarılı olunca yeni oluşan ödeme satırları (yenilenen özetteki yeni paymentId'ler) bu işaretle "Uygulanan tahsilatlar" listesinde gösterilir. İşaret yalnız bu sekmede, sayfa yenilenene kadar yaşar; sayfa yenilenince ya da başka kasada açılınca eski satırlarda işaret görünmez (sunucu kaydı tutmaz). RESULT.md bunu açık risk olarak yazar.

## Owned surface

- `evidence/V1-RMD-1091/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - tests/Clients/StaticApps/split-payment-payer-marker.test.js
  - src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.js
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi
3. İstemci üretim derlemesi (client-build-gate)

## Out of scope

- Sunucu sözleşmesi ve veritabanı; fiş ve rapor çıktıları; ürüne göre bölme ekranı

## Dependencies

- V1-RMD-1090

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. client-build-gate çıktısı exit code 0
3. İki ardışık bağımsız qa-* incelemesi

## Handoff

- None
