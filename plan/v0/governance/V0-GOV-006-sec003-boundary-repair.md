# V0-GOV-006 - Repair V1-SEC-003 ownership boundary

- Task ID: V0-GOV-006
- Status: Done
- Assignee: /root
- Work type: documentation
- Surface state: Existing

## Source basis

- CORR:C35
- CORR:C36

## Goal

V1-SEC-003 icin gerekli host parser ve mevcut regression testinin sahipligini,
tamamlanmis V1-FND-004 yuzeyinden keskin bicimde devretmek.

## Owned surface

- `plan/v1/foundation/V1-FND-004-host-migration-composition.md`
- `plan/v1/security-foundation/V1-SEC-003-host-database-secret-input.md`
- `evidence/V0-GOV-006/**`

## In scope

- `src/Host/Program.cs` ve `MigrationExecutionTests.cs` yuzeylerini V1-SEC-003
  sahibi yapmak.
- V1-FND-004 kapsamındaki test sahipliğini kalan kesin dosyalara daraltmak.

## Out of scope

- Host davranisi, test mantigi, dependency sonucu veya gate durumu degistirmek.

## Dependencies

- V0-GOV-004

## Deliverables

- C35 düzeltmesinin tüm gerekli dosyalar için tek-sahip plan kaydı.

## Acceptance evidence

- Iki task arasinda `Program.cs` veya `MigrationExecutionTests.cs` overlap'i kalmaz.
- Plan denetleyicisi sahiplik çakışması hatası vermez.

## Handoff

- V1-SEC-003
