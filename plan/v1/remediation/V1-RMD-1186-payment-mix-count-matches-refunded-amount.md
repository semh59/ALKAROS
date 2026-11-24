# V1-RMD-1186 - Günlük raporda ödeme yöntemi adedi tamamen iade edilen ödemeyi düşer

- Task ID: V1-RMD-1186
- Status: InProgress
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

Kasiyer denemesi 17 (bulgu 3): `PostgresPaymentSettlementReportRepository.GetPaymentMixAsync` adedi pencerede onaylanan her ödemeyi sayar (iade edilmiş olsa da), tutarı ise iadeyi iade günü düşer; ₺170 ve ₺50 harici POS ödemesinden ₺170'lik olan iade edilince rapor "2 adet ₺50,00" gösterir. Karar: adet de tutar gibi net olur. Bir ödeme tamamen iade edildiği (iade toplamı onaylanan tutara ulaştığı) pencerede o yöntemin adedinden bir düşülür: aynı gün alınıp iade edilen ödeme adetten de tutardan da çıkar, başka gün iade edilen ödeme iade gününde ikisini de düşürür; kısmi iade adedi değiştirmez; çok sayıda iade tek düşüş sayılır. Ayrı satır yerine bu seçilir: tutar zaten net raporlanıyor, rapor sözleşmesi (`PaymentMixEntry`) ve ekran sütunları değişmez. Sözleşme yorumu bu tanımı yazar.

## Owned surface

- `evidence/V1-RMD-1186/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Modules/Reporting/Payments/PostgresPaymentSettlementReportRepository.cs
  - src/Modules/Reporting/Payments/ReportingModels.cs
  - tests/Modules/Reporting/Payments/PaymentSettlementReportTests.cs
  - tests/Modules/Reporting/Payments/PaymentRefundNettingTests.cs
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test (aynı gün tam iade: adet ve tutar net; farklı gün tam iade; kısmi iade adedi değiştirmez; birden çok iade tek düşüş), düzeltme, mutasyon (`cp` + `cmp`)
2. İki bağımsız qa incelemesi
3. Reporting ödeme testleri ve ApiConventions mimari testi

## Out of scope

- Rapor sözleşmesine yeni alan; iade net tutarı bölümü ("henüz kullanılamıyor"); istemci

## Dependencies

- V1-RMD-1185

## Acceptance evidence

1. Kırmızı, yeşil ve mutant çıktıları; sonuç gerekçesi
2. Reporting ödeme testleri ve ApiConventions exit code 0
3. İki ardışık bağımsız qa-* incelemesi

## Handoff

- None
