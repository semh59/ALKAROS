# V1-RMD-918 - Faturasız kuyruğa alınıp sonradan fatura alan müşterinin eski bekleyen kaydı yeniden değerlendirilir

- Task ID: V1-RMD-918
- Status: InProgress
- Assignee: claude-code-session_019vXeYXYQSTiLW1J4UDYBY6
- Work type: implementation
- Surface state: Existing

## Goal

V1-RMD-916 sonrası: faturasız (`CustomerProfile`) kuyruğa alınmış bir müşteri sonradan fatura alırsa bekleyen iş kaydı kalır; anonimleştirme koruması onu engeller ama kayıt her çalışmada "engellendi" sayılır, `blocked` şişer ve kayıt hiç kapanmaz (iş kayıtları silinemez, yalnız Pending'den Done'a gider). Karar: bekleyen kayıtlar okunurken öznenin hâlâ o sınıfa ait olup olmadığı yeniden değerlendirilir; artık faturası olan müşterinin `CustomerProfile` kaydı işlenmez ve sayılmaz (kayıt değişmeden durur, müşteri 10 yıl dolunca `InvoicedCustomerProfile` olarak yeni kayıtla seçilir). Bakiye veya fatura koruması anonimleştirme planında ayrıca kalır (yarış güvenliği).

## Owned surface

- `evidence/V1-RMD-918/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Modules/Privacy/RetentionExecution/PostgresRetentionExecutionService.cs
  - src/Modules/Privacy/RetentionExecution/RetentionSelection.cs
  - tests/Host/MigrationComposition/Program/KvkkRetentionTests.cs
  - tests/Modules/Privacy/RetentionExecution/RetentionExecutionTests.cs
  - docs/compliance/kvkk-retention-runbook.md
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test: faturasız kuyruğa alınan müşteri fatura alır, çalıştırma `blocked=0` verir ve profil anonimleşmez
2. Bekleyen kayıt okumasında sınıf üyeliği yeniden değerlendirmesi
3. Mutasyon ve iki bağımsız qa incelemesi; el kitabı notu

## Out of scope

- İş kaydı silme veya Done'a çekme (kayıtlar değişmez tasarım)
- Bakiye nedeniyle engelli kayıtların davranışı

## Dependencies

- V1-RMD-917

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
