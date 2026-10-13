# V1-RMD-507 - Ödeme iade defteri (payment_reversals) ve iade sonrası raporlama

- Task ID: V1-RMD-507
- Status: Planned
- Assignee: Unassigned
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-10-13

## Goal

İade akışının veri tabanı ve alan temeli (`docs/domain/refund-ledger.md`): değişmez `payments.payment_reversals` defteri, `Payment` için `Approved → Refunded` geçişi, bunu engelleyen DB kısıtının gevşetilmesi ve gün sonu ile ödeme dağılımı raporlarının iadeyi iade gününde düşmesi. Hesabın tüm ödemeleri iade edilince hesap iptal edildiği için (V1-RMD-508) hesap kapanış hesaplayıcısı ve tahsilat toplamları değişmez; kısmi iade bu görevde yoktur (defter `PartialRefund` türünü taşır, servis yalnız tam iade yapar).

## Owned surface

- `plan/v1/remediation/V1-RMD-507-payment-reversal-ledger-and-netting.md`
- `evidence/V1-RMD-507/**`
- `database/migrations/V1/V1-RMD-507/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Payments/PaymentAggregate/Payment.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Payments/PaymentAggregate/PostgresPaymentReversalRepository.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Reporting/BusinessDayTotals/BusinessDayTotalsReader.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Reporting/Payments/PostgresPaymentSettlementReportRepository.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Payments/PaymentAggregate/PaymentReversalTests.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Payments/PaymentAggregate/ALKAROS.Payments.PaymentAggregate.Tests.csproj
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Reporting/Payments/PaymentRefundNettingTests.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Reporting/Payments/ALKAROS.Reporting.Payments.Tests.csproj
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Reporting/V1Operations/BusinessDayRefundNettingTests.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Reporting/V1Operations/ALKAROS.Reporting.V1Operations.Tests.csproj
- Sınırlı ek (paylaşılan, geri-tik olmadan): database/MigrationComposition/order.json
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/MigrationComposition/Manifest/ManifestTests.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Composition/Migrations/MigrationManifest.cs
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Migration 182: `payments.payment_reversals` (değişmez, tutar > 0, ödeme başına toplam ≤ ödeme tutarı), `ck_payments_status_approved_amount_pairing` kısıtının `Approved`, `Refunded`, `PartiallyRefunded` durumlarını kapsayacak şekilde gevşetilmesi.
- `Payment` iade geçişleri (`Approved → Refunded | PartiallyRefunded`, `PartiallyRefunded → Refunded`); `Unknown`/`ReconciliationRequired` ödeme iade edilemez.
- Tek transaction'da defter satırı ekleyip ödeme durumunu ilerleten depo (aşırı iade ve çift iade reddi, idempotency anahtarı).
- Gün sonu cirosu ve ödeme dağılımı: iade edilmiş ödemeler orijinal gününde sayılır, iade tutarı iade gününde düşülür.

## Out of scope

- İade isteği/onayı, nakit defteri satırı, kart elle iade kaydı, hesap iptali (V1-RMD-508); arayüz (V1-RMD-509); kısmi iade servisi.

## Dependencies

- V1-RMD-506

## Acceptance evidence

- Testler ve mutasyon kanıtı; çıktılar `evidence/V1-RMD-507/` altındadır.

## Handoff

- None
