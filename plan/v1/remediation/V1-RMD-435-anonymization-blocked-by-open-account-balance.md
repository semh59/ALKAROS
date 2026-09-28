# V1-RMD-435 - Açık cari bakiyesi olan müşterinin anonimleştirilmesinin engellenmesi

- Task ID: V1-RMD-435
- Status: InProgress
- Assignee: claude-code-session_012a6DqV4367gGp1Uk8TUam1
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-28

## Goal

V1-RMD-393 para akışı denetimi F-13 (ilk yarı): KVKK anonimleştirme talebini finansal kayıt açısından kontrol eden
`IAnonymizationRetentionGuard` için üretimde `NoKnownBlockingReferencesGuard` kayıtlıydı; hiçbir müşteriyi
engellemiyordu. Gerekçesi "cari hesap modülü (V14-ACC) henüz yok" idi; V14-ACC-001..003 Done olduğu için gerekçe
geçersiz. Bugün anonimleştirmeyi çağıran bir uç nokta yok, ama ilk çağıran açıldığı anda borcu olan müşterinin
kimliği silinebilirdi.

Bu görev: yer tutucu kaldırılır; yerine cari hesap bakiyesini okuyan gerçek bir koruma gelir. Müşterinin cari
bakiyesi sıfır değilse (müşteri borçluysa ya da müşteriye borçluysak) anonimleştirme talebi `RetentionBlocked`
olur ve nedeni Türkçe yazılır; bakiye kapanınca mevcut `ReevaluateAsync` talebi yeniden açar. Hiç cari hareketi
olmayan müşteri engellenmez.

## Owned surface

- `plan/v1/remediation/V1-RMD-435-anonymization-blocked-by-open-account-balance.md`
- `evidence/V1-RMD-435/**`
- `src/Modules/CustomerAccounts/Retention/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/CustomerData/AnonymizationState/IAnonymizationRetentionGuard.cs
  (V14-CST-002 sahipliğinde) — yalnız yer tutucu sınıfın kaldırılması ve belge yorumu
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/CustomerData/CustomerDataModule.cs (V14-CST-001
  sahipliğinde) — yalnız yer tutucu kaydının kaldırılması
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/CustomerAccounts/BillCharges/CustomerAccountsBillChargesModule.cs
  (V14-ACC-003 sahipliğinde) — yalnız korumanın kaydı
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/CustomerAccounts/BillCharges/OutstandingBalanceRetentionGuardTests.cs
  (V14-ACC-003 test projesinde yeni dosya)

## In scope

- `OutstandingBalanceRetentionGuard`: `IAccountBalanceProjection` üzerinden bakiye okuması ve Türkçe engel nedeni.
- Korumanın `CustomerAccounts.BillCharges` modülünde kaydı (bu modül zaten CustomerData ve CustomerAccounts'a
  bağımlı; yeni modül bağımlılığı eklenmez).

## Out of scope

- Fatura (V14-INV) kaynaklı saklama engeli: fatura modülü henüz yok; eklendiğinde kendi korumasını getirir.
- Anonimleştirme için HTTP uç noktası.

## Dependencies

- V1-RMD-434

## Acceptance evidence

- Yeni testler (`OutstandingBalanceRetentionGuardTests`, gerçek PostgreSQL 18): borçlu müşteri engellenir, müşteriye
  borçlu olduğumuz durum engellenir, hareketi olmayan ya da bakiyesi kapanmış müşteri engellenmez; üretim modül
  kataloğunda `IAnonymizationRetentionGuard` bu korumaya çözülür. Yer tutucu geri konunca kırmızı
  (`evidence/V1-RMD-435/red-without-fix.log`).
- `ALKAROS.CustomerAccounts.BillCharges.Tests`, `ALKAROS.CustomerData.AnonymizationState.Tests`, mimari ve
  kompozisyon testleri geçer (`evidence/V1-RMD-435/tests.log`).
- Semih'in elle deneyebileceği senaryo: yok; anonimleştirmeyi açan bir ekran veya uç nokta henüz yok. İlk açıldığında
  borcu kapanmamış müşterinin talebi "saklama engeli" durumunda kalır.

## Handoff

- None
