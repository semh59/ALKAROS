# V0-GOV-015 - Make migrations atomic and history-backed

- Task ID: V0-GOV-015
- Status: Done
- Assignee: codex-v0-gov-015
- Work type: implementation
- Surface state: Existing

## Source basis

- CORR:C37

## Goal

Her migration scriptini applied history kaydi ile ayni PostgreSQL transaction'
inda calistirmak; yeniden calistirmada checksum eslesmesini zorunlu tutmak ve
uygulanmamis veya sonraki position varken rollback'i reddetmek.

## Owned surface

- HostComposition.cs sahipliği V1-FND-013'e devredilmiştir (C42); bu görev artık bu path'i yazamaz.
- `src/Host/Composition/Migrations/MigrationExecutor.cs`
- `src/Host/Composition/Migrations/MigrationHistoryStore.cs`
- `tests/Host/MigrationComposition/History/MigrationHistoryTests.cs`
- `evidence/V0-GOV-015/**`
- C52 Host composition service-registration and migration-secret remediation surfaces are transferred to V1-FND-017 and V1-SEC-004; this historical task remains closed.

## In scope

- `psql --single-transaction`, control-table bootstrap, applied position ve
  SHA-256 kaydi, re-run skip/checksum rejection ve rollback precondition.

## Out of scope

- Eski migration SQL dosyalarini yeniden yazmak, yeni product schema eklemek,
  migration manifest siralamasini degistirmek veya production deployment.

## Dependencies

- V0-GOV-012
- V1-FND-004

## Deliverables

- Atomic migration/history execution contract'i ve PostgreSQL integration
  testleri.

## Acceptance evidence

- Basarisiz script schema veya history satiri birakmaz.
- Ayni checksum tekrar calismaz; farkli checksum fail-closed olarak reddedilir.
- Uygulanmamis veya daha sonraki position mevcutken rollback reddedilir.
- Test projesi basariyla tamamlanir.

## Handoff

- V15-BKP-002
