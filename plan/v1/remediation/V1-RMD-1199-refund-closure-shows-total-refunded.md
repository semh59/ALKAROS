# V1-RMD-1199 - İade ile kapanışta ekranlar toplam iade edilen tutarı gösterir

- Task ID: V1-RMD-1199
- Status: Planned
- Assignee: Unassigned
- Work type: implementation
- Surface state: Existing

## Goal

Kasiyer denemesi 18 (bulgu 7 devamı): kısmi iadelerden sonra hesabı kapatan son iadede ödeme sayfasının "İade Tamamlandı" ekranı yalnız son iadenin tutarını yazar ("X iade edildi" toplam gibi okunur). Ekran, hesabın tamamlanmış iade isteklerinin toplamını yazar (liste okunamazsa son iadenin tutarı). Müşteri ekranı iade kapanışında "İade tamamlandı. X ₺ iade edildi." der; X, iptal edilen hesapların `Refunded` ödemelerinin onaylı tutarları toplamıdır (hesap yalnız hiçbir ödeme para tutmuyorsa iade ile kapanır, bu yüzden her ödemenin tamamı iade edilmiştir).

## Owned surface

- `evidence/V1-RMD-1199/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Host/DualScreen/CustomerDisplayPaymentProgress.cs
  - src/Host/DualScreen/DualScreenApplication.Payments.cs
  - tests/Host/MigrationComposition/DualScreen/CustomerDisplayStatementTests.cs
  - src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.js
  - tests/Clients/StaticApps/split-payment-refunds.test.js
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test (iki ayrı iadenin toplamı, liste okunamazsa son tutar, müşteri ekranı toplam metni), düzeltme, mutasyon (`cp` + `cmp`)
2. İki bağımsız qa incelemesi
3. İlgili Host ve StaticApps testleri, istemci üretim derlemesi

## Out of scope

- Sunucu iade yanıtına yeni alan eklemek; iade akışı kuralları

## Dependencies

- V1-RMD-1198

## Acceptance evidence

1. Kırmızı, yeşil ve mutant çıktıları; sonuç gerekçesi
2. client-build-gate çıktısı exit code 0
3. İki ardışık bağımsız qa-* incelemesi

## Handoff

- None
