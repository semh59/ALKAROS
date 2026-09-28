# V14-RMD-001 - Anonimleştirme zaman damgası testi PostgreSQL hassasiyetinde çalışsın

- Task ID: V14-RMD-001
- Status: Done
- Assignee: claude-code-session_012a6DqV4367gGp1Uk8TUam1
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-28

## Goal

`UpdateStatusToAnonymizedSetsAnonymizedAt`, `DateTimeOffset.UtcNow`'u (100 ns tick) PostgreSQL `timestamptz`'ye
(1 µs) yazıp geri okuyor ve tam eşitlik bekliyor; tick'in son hanesi sıfır değilse test düşüyor, yani çoğu
koşuda kırmızı (V1-RMD-393 F-03). Test, depolanabilir (mikrosaniyeye kesilmiş) bir değer kullanır; gidiş-dönüş
için tam eşitlik korunur.

## Owned surface

- `plan/v1.4/customer-data/V14-RMD-001-anonymized-at-test-precision.md`
- Sınırlı ek (V14-CST-002 sahipliğinde kalır):
  tests/Modules/CustomerData/AnonymizationState/PostgresCustomerAnonymizationRequestStoreTests.cs

## Dependencies

- V14-CST-002
- V1-RMD-394

## Acceptance evidence

- `dotnet test tests/Modules/CustomerData/AnonymizationState` 20 ardışık koşuda exit 0.
