# V1-RMD-863 - Ödeme uzlaşma raporu: yöntem ayrımı (cari hesap, elle kart onayı, reddedilen kart denemesi) ve çoklu denemede katlanan toplam

- Task ID: V1-RMD-863
- Status: Planned
- Assignee: claude-code-session_01FMN5WTVUmS2xYXu1gL4Tsz
- Work type: implementation
- Surface state: Existing

## Goal

G10 (F0094, F0095, F0450): PostgresPaymentSettlementReportRepository ödeme yöntemini eleme ile çıkarır ve iki gerçek hata üretir. (1) card_settlement_attempts tablosu LEFT JOIN edilir ama payment_id tekil değildir; bir ödemenin birden çok denemesi (reddedilmiş + onaylanmış) ödeme adedini ve tutarı katlar, iade toplamını da katlar. (2) Reddedilmiş ya da uzlaşma gerektiren tek bir kart denemesi, kart olmayan ödemeyi "BankCard" yapar; cari hesaba yazılan ödeme ile elle onaylanan kart ödemesi hiç tanınmaz ve "Eft" sayılır. Faturalama tarafındaki PaymentMethodLookup bu ayrımı zaten doğru yapar (cari, nakit, onaylı kart denemesi ya da onaylı elle onay, kalan EFT). Rapor aynı kuralı EXISTS ile uygular (join olmaz, katlama olmaz); cari hesap "CustomerAccount" olarak çıkar (ekrandaki Türkçe karşılığı zaten var). GROUP BY üzerindeki anlamsız LIMIT kaldırılır (en çok dört yöntem grubu çıkar).

## Owned surface

- `evidence/V1-RMD-863/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Modules/Reporting/Payments/PostgresPaymentSettlementReportRepository.cs — yöntem ayrımı, EXISTS, LIMIT kaldırma.
  - src/Modules/Reporting/Payments/ReportingModels.cs — PaymentMixEntry açıklaması.
  - tests/Modules/Reporting/Payments/PaymentSettlementReportTests.cs — yeni testler.
  - tests/Modules/Reporting/Payments/PaymentRefundNettingTests.cs — iade katlama testi.
  - tests/Modules/Reporting/Payments/Fixtures/PaymentReportTestDatabase.cs — tohum yardımcıları.
  - tests/Modules/Reporting/Payments/ALKAROS.Reporting.Payments.Tests.csproj — iki migration dosyası.
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Yöntem kuralı: Cari hesap (Charge işlemi), nakit satış, onaylı kart denemesi ya da onaylı elle kart onayı, kalan EFT. Kural PaymentMethodLookup ile aynı; modül sınırı yüzünden kopya kalır ve testle sabitlenir.
2. İade toplamı da aynı kuralı kullanır, katlanmaz.
3. Testler: iki denemeli ödeme bir kez sayılır; reddedilmiş deneme yöntemi değiştirmez; cari ve elle onaylı kart ayrı çıkar; iade toplamı katlanmaz.
4. Mutasyon: onay koşulu, EXISTS yerine JOIN, cari dalı, elle onay dalı (cp+cmp ile geri alınır).

## Out of scope

- payments tablosuna yöntem sütunu eklemek (migration); kural bugünkü kaynak kayıtlarını okur.
- Rapor ekranı (etiket zaten var).

## Dependencies

- None

## Acceptance evidence

1. Reporting.Payments testleri gerçek Postgres (alkaros-test-pg, 55432) ile exit 0; çıktılar `evidence/V1-RMD-863/`.
2. Mutasyon kanıtı cp+cmp ile geri alınmış.
3. Plan audit ve consistency audit 0 hata/0 uyarı; kapsam aracı exit 0.
4. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
