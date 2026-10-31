# V1-RMD-764 - Anonimleştirme yasal bekletmeyi veriyi silmeden önce denetler

- Task ID: V1-RMD-764
- Status: InProgress
- Assignee: claude-code-session_01FMN5WVXi1HJCTEk5argkgZ
- Work type: remediation
- Surface state: Existing

## Goal

V1-RMD-739 bulgusu V15-KVK-002: `PostgresAnonymizationWorkflow.ProcessAsync` kişiyi listelendikten sonra adımları yazar; yasal bekletme ancak iş kalemi tamamlanırken (`CompleteAsync`) yakalanır. Liste ile yazım arasında bir yasal bekletme konursa veri çoktan silinmiş olur, iş yalnız "Blocked" olur. Mevcut test (`AHoldPlacedWhileTheJobRunsStopsItBeforeTheWorkItemIsCompleted`) işin Blocked olduğunu doğrular, verinin yine de silindiğini doğrulamaz.

Her adımın yazımından önce, aynı transaction ve aynı hesap kilidi içinde `privacy.legal_holds` içinde etkin bekletme aranır; varsa hiçbir şey yazılmaz, iş "legal hold" ile Blocked olur.

## Owned surface

- `plan/v1/remediation/V1-RMD-764-anonimlestirme-yasal-bekletme-yazimdan-once.md`
- `evidence/V1-RMD-764/**`
- Sınırlı ek: src/Modules/Privacy/Anonymization/PostgresAnonymizationWorkflow.cs
- Sınırlı ek: tests/Modules/Privacy/Anonymization/AnonymizationWorkflowTests.cs

## In scope

- `ApplyStepAsync` yazımdan önce etkin yasal bekletmeyi arar ve bulursa `RetentionSubjectHeldException` ile durur; `ProcessAsync` bunu "legal hold" Blocked olarak kaydeder.
- Gerçek PostgreSQL testi: iş açıldıktan sonra konan bekletme verinin, kontrol noktalarının ve bekleyen iş kaleminin değişmemesini sağlar.

## Out of scope

- Bekletme koyma servisi ve bekletmenin kilitle serileştirilmesi (kontrol ile yazım arasındaki dar pencere kalır).
- Yakın hareket ve bekleyen talep yeniden denetimi (KVK-001 devir notunun diğer yarısı).
- İş kalemi tamamlama (`CompleteAsync`) davranışı.

## Dependencies

- V15-KVK-001

## Acceptance evidence

1. Düzeltmeden önce yeni test kırmızı olur (veri silinir); çıktı kaydedilir.
2. Düzeltmeden sonra `ALKAROS.Privacy.Anonymization.Tests` (gerçek PostgreSQL) exit 0 geçer.
3. Mutasyon: bekletme denetimi kaldırılınca yeni test kırmızı olur; dosya hash'i geri yüklenir.
4. Kapsam aracı InProgress baseline'a karşı exit 0 verir; iki bağımsız incelemede artık bulgu kalmaz.

## Handoff

- None
