# V1-RMD-498 - KVKK anonimleştirme eşzamanlılığı ve tedarikçi saklama tabanı

- Task ID: V1-RMD-498
- Status: InProgress
- Assignee: claude-code-session_01UKcG8ALDsRweQYx6F2bSYe
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-10-01

## Goal

Bağımsız taramanın bulduğu üç KVKK riskini kapatmak: aynı işi iki işçinin eşzamanlı çalıştırmasında iş kaydının çökmesi, müşteri anonimleştirmesi ile hesaba yazma arasında kilit olmaması (bakiye kontrolünden sonra borç yazılıp profilin silinebilmesi), ve yalnız alış faturasıyla çalışılan tedarikçinin saklama süresinin faturaları saymaması.

## Owned surface

- `plan/v1/remediation/V1-RMD-498-kvkk-concurrency-and-retention-basis.md`
- `evidence/V1-RMD-498/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Privacy/Anonymization/PostgresAnonymizationWorkflow.cs ve tests/Modules/Privacy/Anonymization/AnonymizationWorkflowTests.cs — yalnız adım kilidi ve yeniden doğrulama
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Privacy/RetentionExecution/RetentionSelection.cs, tests/Modules/Privacy/RetentionExecution/RetentionExecutionTests.cs ve tests/Modules/Privacy/RetentionExecution/Fixtures/RetentionTestDatabase.cs — yalnız tedarikçi saklama tabanı
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Her anonimleştirme adımı, işe özgü bir kilit altında ve adım kaydı yeniden okunarak çalışır; aynı adımı başka işçi bitirmişse atlanır. İş zaten tamamlanmışsa hata durumu onu bozmaz.
- Adım işleminde müşteri hesabına yazma kilidi (`account-charge-customer`) alınır ve bakiye engeli kilit altında yeniden değerlendirilir; açık bakiye varsa adım yazmadan durur.
- Tedarikçi saklama süresi son alış faturasını ve mal kabulünü de sayar.

## Out of scope

- Anonimleştirme kurallarının veya saklama sürelerinin değiştirilmesi; müşteri anonimleştirme isteği için HTTP ucu.

## Dependencies

- V15-KVK-002

## Acceptance evidence

- Testler ve mutasyon kanıtı; çıktılar `evidence/V1-RMD-498/` altındadır.

## Handoff

- None
