# V1-FND-005 — Kusur 5 Kapanış Kanıtı (2026-08-03)

Kapsam: EXECUTION_READY_PLAN.md Aşama 2, satır 5 — "Database dışı resource
DB commit'ten önce commit olabiliyor; tam atomiklik iddiası yanlış."

## Değişen yollar

| Dosya | Değişiklik |
| --- | --- |
| `src/BuildingBlocks/Transactions/ITransactionResource.cs` | DB overload'ına fail-closed default gövde: `CommitAsync(DbConnection, DbTransaction, CancellationToken)` → `throw new InvalidOperationException("...external side effects must be written as outbox messages...")`; parametresiz `CommitAsync(CancellationToken)` imzası korundu |
| `src/BuildingBlocks/Transactions/TransactionContext.cs` | Doc: external side-effect taşıma contract'ı (outbox/post-commit) |
| `src/BuildingBlocks/Transactions/TransactionOutbox.cs` (ve Resource) | Doc güncellemeleri; xUnit doctag'leri düzeltildi (Â§ → §) |
| `tests/BuildingBlocks/TestHelpers/Fixtures/RecordingResource.cs` | İkinci DB overload'a yönlendirir |
| `tests/BuildingBlocks/TransactionOutboxIntegration/TransactionOutboxIntegrationTests.cs` | Yeni test: `DatabaseScopedCommitRejectsResourceWithoutDatabaseCommit` |

## Davranış

- DB commit kapsamında yalnız DB overload'ı olan bir resource enlist edilirse
  `InvalidOperationException` fırlar ve yan etki gerçekleşmez — external side
  effect'ler outbox mesajlarıyla taşınmak zorundadır.
- Parametresiz imza korunur (yalnız DB dışı atomiklik gerektirmeyen resource'lar).

## Test kanıtı

Komut (tam suite içinden):

```console
Başarılı!  - Başarısız:     0, Başarılı:    12, Atlanan:     0, Toplam:    12, Süre: 1 s - ALKAROS.TransactionOutboxIntegration.Tests.dll (net8.0)
Başarılı!  - Başarısız:     0, Başarılı:    25, Atlanan:     0, Toplam:    25, Süre: 380 ms - ALKAROS.Transactions.Tests.dll (net8.0)
```

TransactionOutboxIntegration 11 → 12 (yeni test: DB'li `RunAsync` içinde
`ExternalSideEffectResource` enlist → InvalidOperationException + side effect
yok).

Build: 0 Uyarı, 0 Hata, EXIT=0.

## Kapanış doğrulaması

- Kapanış ölçütü: "External side-effect outbox/post-commit contract'a taşınır;
  failure testleri geçer." — 12/12 + 25/25 geçti.
- Write-set yalnız yukarıdaki yollardadır. Commit bu kanıt dosyalarıyla birlikte push edildi (2026-08-03).
