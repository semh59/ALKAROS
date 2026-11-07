# V1-RMD-882 - Cari bakiye önbelleği tutarlılık denetimi ve onarımı (RebuildAsync gerçek akışa bağlanır)

- Task ID: V1-RMD-882
- Status: InProgress
- Assignee: claude-code-session_01FMN5WTVUmS2xYXu1gL4Tsz
- Work type: implementation
- Surface state: Existing

## Goal

`IAccountBalanceProjection.RebuildAsync` üretimde hiçbir yerden çağrılmıyordu (ölü kod). V0-DAT-004 her projeksiyon için drift dedektörü ve yeniden kurulum yolu ister; silmek bu kuralı bozar. Günlük bakım işi (`balance-reconcile`, Güvenlik/Sistem ekranından elle de çalışır) defter toplamı ile önbellek bakiyesi farklı olan müşterileri bulur ve `RebuildAsync` ile onarır; kaç müşterinin onarıldığı iş sonucunda görünür. Her turda en fazla 500 müşteri onarılır, kalanlar sonraki turda.

## Owned surface

- `evidence/V1-RMD-882/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Host/Experience/CustomerAging/BalanceReconcileMaintenanceJob.cs — onarım işi
  - tests/Host/Experience/CustomerAccounts/BalanceReconcileJobHttpTests.cs
  - src/Modules/CustomerAccounts/BalanceProjection/IAccountBalanceProjection.cs — sapmış müşterileri bulma
  - src/Modules/CustomerAccounts/BalanceProjection/PostgresAccountBalanceProjection.cs
  - src/Modules/CustomerAccounts/BalanceProjection/CustomerAccountBalance.cs
  - src/Host/Experience/CustomerAging/ReceivablesAgingEndpoints.cs — iş kaydı
  - tests/Modules/CustomerAccounts/BalanceProjection/PostgresAccountBalanceProjectionTests.cs
  - tests/Host/Experience/CustomerAccounts/CustomerAccountsHttpTestDatabase.cs
  - tests/Host/Experience/CustomerAccounts/ALKAROS.Host.Experience.CustomerAccounts.Tests.csproj
  - tests/Host/Experience/SecurityAdministration/SecurityAdministrationHttpTests.cs — bakım işi listesi
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Sapma sorgusu (defter toplamı <> önbellek bakiyesi, en fazla 500 müşteri) ve onarım işi
2. Gerçek Postgres testi: bozulmuş satır onarılır, tutarlı satıra dokunulmaz, iş kayıtlıdır
3. Mutasyon ve iki bağımsız qa incelemesi

## Out of scope

- Önbellek yazım yolunun (trigger) değişmesi, yaşlandırma raporu

## Dependencies

- None

## Acceptance evidence

1. Kırmızı/yeşil test çıktıları
2. Mutasyon sonuçları
3. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
