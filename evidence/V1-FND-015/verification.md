# V1-FND-015 verification

Task: `V1-FND-015` — mandate inbox side-effect idempotency contract
(PDF:I.11-I.15, C42).
Date: 2026-08-04
Repo: `https://github.com/semh59/ALKAROS.git` branch `master`.

## Değişiklikler

- `src/BuildingBlocks/Messaging/IInboxHandler.cs`: yazılı idempotency
  sözleşmesi — aynı mesaj birden fazla teslim edilir (retry veya expired
  lease yeniden teslimi); implementasyon tekrar-teslimde ikinci yan etki
  üretmekle YÜKÜMLÜ DEĞİL, üretmemekle yükümlüdür; deduplikasyon anahtarı
  (Source, ExternalEventId); `AttemptCount` teslim başına deneme bilgisini
  taşır.
- `src/BuildingBlocks/Messaging/InboxMessage.cs`: `AttemptCount`
  (ilk teslimde 0, sonraki teslimlerde kayıtlı başarısız deneme sayısı),
  `LastError`, `Status` yeniden işleme bilgisi olarak sözleşmeye bağlandı.
- `tests/BuildingBlocks/Idempotency/InboxRedeliveryContractTests.cs` (yeni):
  - Retry yolu: ack kaybı simülasyonunda aynı mesaj 2 teslim, ikinci
    teslimde `AttemptCount=1`, idempotency anahtarı ile yan etki 1 kez,
    son durum `processed`, `attempt_count=1`.
  - Lease expiry yolu: başarısız deneme sonrası mesaj `in_flight` + süresi
    dolmuş lease'e zorlanır (çöken worker senaryosu), yeniden teslim aynı
    mesajı `AttemptCount=1` ile getirir ve ikinci yan etki üretilmez.

## Komutlar ve exit code'lar

### Hedefli koşu

```console
> dotnet test ALKAROS.slnx --filter "FullyQualifiedName~InboxRedeliveryContract"
Başarılı! - Başarısız: 0, Başarılı: 2, Toplam: 2 - ALKAROS.Idempotency.Tests.dll
exit=0
```

İlk koşuda `SingleMessageId` getter'ı ikinci teslimattan sonra çağrıldığı
için `Assert.Single` patladı; id ilk teslimatta yakalanıp sonrasında sabit
kullanıldı → 2/2 geçti.

### Tam çözüm — 3 ardışık koşu

```console
> dotnet test ALKAROS.slnx --no-restore -v q   (run 1,2,3)
run1=0  run2=0  run3=0
Transcripts: full-run-1.txt, full-run-2.txt, full-run-3.txt
```

### Kapsam ve plan doğrulaması

```console
> py tools/task-scope/task_scope_tool.py --task-id V1-FND-015 --format text
OK: All changes within scope for V1-FND-015
exit=0

> py tools/plan-audit/plan_audit_tool.py validate            -> errors 0 (288 md, 267 task, 939 edge)
> py tools/plan-audit/plan_audit_tool.py validate-coverage   -> errors 0
> py tools/plan-audit/plan_audit_tool.py verify-manifest     -> errors 0 (404 md, added-file hashes 192)
```

## Kapanış notları

- `plan/AUDIT_REPORT.md` + `plan/AUDIT_MANIFEST.json`: yalnız araç yeniden
  üretimi (nominal sahipler V1-FND-008/V0-GOV-030); FIND-IA-0046 ve
  V0-GOV-031 emsaliyle geri alınmadı, kayıt düşüldü.
- Son kapsam kontrolü `InProgress` iken alındı; araç `Done` sonrası
  çalıştırılamaz.
