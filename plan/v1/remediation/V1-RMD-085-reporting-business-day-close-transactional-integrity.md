# V1-RMD-085 - Reporting business-day close transactional integrity

- Task ID: V1-RMD-085
- Status: Done
- Assignee: claude-code-019uHsMymyuC1tZ5DHtmWsRi
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-09-01

## Goal

`OperationalReportService.CloseBusinessDayAsync` iş günü kapanışını, garson özetlerini ve baskı hata özetlerini birbirinden ayrı bağlantılarda yazıyor; sıra ortasında bir hata olursa gün kapalı ama özetleri eksik kalır. Kapanış ve tüm özetler tek bir veritabanı transaction'ında yazılır.

## Owned surface

- `plan/v1/remediation/V1-RMD-085-reporting-business-day-close-transactional-integrity.md`
- `src/Modules/Reporting/V1Operations/**`
- `tests/Modules/Reporting/V1Operations/**`
- `evidence/V1-RMD-085/**`

## In scope

- `IOperationalReportRepository` içine iş günü kapanışı ile garson ve baskı hata özetlerini tek transaction'da yazan bir metot eklemek.
- `OperationalReportService.CloseBusinessDayAsync` içinde bu metodu kullanmak; ayrı ayrı yazan çağrı zincirini kaldırmak.
- Sıra ortasında hata durumunda hiçbir yazının kalıcı olmadığını doğrulayan xUnit testi.

## Out of scope

- Ciro, sipariş ve iptal sayılarını modül içinde hesaplamak; bu ayrı bir raporlama görevine aittir.
- Diğer raporlama sorgularını değiştirmek.

## Dependencies

- V1-RMD-084

## Deliverables

- Tek transaction'da iş günü kapanışı ve özet yazımı yapan raporlama servisi.

## Acceptance evidence

- `dotnet test` reporting modülü testleri sıfır hata verir: Docker `alkaros-sdk10-rt8` + `alkaros-pg` üzerinde `ALKAROS.Reporting.V1Operations.Tests` 6/6 geçer.
- Özet yazımı sırasında yapay bir hata enjekte edildiğinde iş gününün kapanmadığını ve özetlerin yazılmadığını doğrulayan `CloseBusinessDayWithSummariesRollsBackWholeCloseWhenSummaryWriteFails` testi geçer.
- Ayrıntılı kanıt: `evidence/V1-RMD-085/close-transaction.md`.

## Handoff

- V1-GOV-047
