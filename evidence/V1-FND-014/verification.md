# V1-FND-014 verification

Task: `V1-FND-014` — harden retry SQL identifier surface (PDF:I.11-I.15, C42).
Date: 2026-08-04
Repo: `https://github.com/semh59/ALKAROS.git` branch `master`.

## Değişiklikler

- `src/BuildingBlocks/Messaging/RetryPolicy.cs`: `AllowedTableNames` kayıtlı
  sabit küme eklendi (`inbox_messages`, `outbox_messages`, `StringComparer.Ordinal`).
  `RecordFailureAsync` artık kayıtlı olmayan herhangi bir `tableName` değerini
  komut kurulmadan önce `ArgumentException` ile fail-closed reddeder
  (boşluk, büyük/küçük harf değişikliği, trailing whitespace ve SQL
  enjeksiyon denemesi dahil). Mevcut çağrıcılar değişmedi:
  `InboxStore` → `"inbox_messages"`, `OutboxStore` → `"outbox_messages"`.
- `tests/BuildingBlocks/Idempotency/RetrySqlIdentifierTests.cs` (yeni):
  allowlist'in yalnız iki kayıtlı kimliği içerdiği; kayıtlı kimliklerin guard'ı
  geçip gerçek komut yürütme aşamasına ulaştığı (kapalı bağlantıda
  `InvalidOperationException`); kayıtsız/SQL-ekli/boşluklu/büyük-harfli
  kimliklerin komut öncesi `ArgumentException` ile reddedildiği.

## Komutlar ve exit code'lar

### Hedefli koşu

```
> dotnet test ALKAROS.slnx --filter "FullyQualifiedName~RetrySqlIdentifier"
Başarılı! - Başarısız: 0, Başarılı: 7, Toplam: 7 - ALKAROS.Idempotency.Tests.dll
exit=0
```

İlk koşuda CA1861 (sabit dizi argümanı) analyzer hatası → beklenti dizisi
`static readonly` alana taşındı → 7/7 geçti.

### Tam çözüm — 3 ardışık koşu

```
> dotnet test ALKAROS.slnx --no-restore -v q   (run 1,2,3)
run1=0  run2=0  run3=0
Transcripts: full-run-1.txt, full-run-2.txt, full-run-3.txt
```

### Kapsam ve plan doğrulaması

```
> py tools/task-scope/task_scope_tool.py --task-id V1-FND-014 --format text
OK: All changes within scope for V1-FND-014
exit=0

> py tools/plan-audit/plan_audit_tool.py validate            -> errors 0 (288 md, 267 task, 939 edge)
> py tools/plan-audit/plan_audit_tool.py validate-coverage   -> errors 0
> py tools/plan-audit/plan_audit_tool.py verify-manifest     -> errors 0 (403 md, added-file hashes 191)
```

## Kapanış notları

- `plan/AUDIT_REPORT.md` + `plan/AUDIT_MANIFEST.json`: yalnız araç yeniden
  üretimi (nominal sahipler V1-FND-008/V0-GOV-030); FIND-IA-0046 ve
  V0-GOV-031 emsaliyle geri alınmadı, kayıt düşüldü.
- Son kapsam kontrolü `InProgress` iken alındı; araç `Done` sonrası
  çalıştırılamaz.
