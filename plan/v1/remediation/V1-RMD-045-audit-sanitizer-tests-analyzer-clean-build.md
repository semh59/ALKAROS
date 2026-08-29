# V1-RMD-045 - Audit sanitizer tests analyzer clean build

- Task ID: V1-RMD-045
- Status: Done
- Assignee: a04804b8-a15d-498a-9753-7b7c3a0e27b3
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-08-29

## Goal

`AuditSanitizerTests.cs` içindeki test metot isimlerinde alt çizgi kullanımından kaynaklanan dört `CA1707` analyzer hatasını düzeltmek ve tüm testlerin `--warnaserror` ile temiz derlenmesini sağlamak.

## Owned surface

- `plan/v1/remediation/V1-RMD-045-audit-sanitizer-tests-analyzer-clean-build.md`
- `tests/Modules/Audit/EventStore/AuditSanitizerTests.cs`
- `evidence/V1-RMD-045/**`

## In scope

- Test metot isimlerini proje kodlama standartlarına ve `CA1707` analyzer kuralına tam uyumlu hale getirmek.
- Audit sanitizasyon testlerini eksiksiz olarak korumak ve güvenli şekilde doğrulamak.

## Out of scope

- Production sanitizer mantığını ve domain kurallarını değiştirmek.

## Dependencies

- V1-RMD-044

## Deliverables

- `CA1707` kural ihlali içermeyen, temiz xUnit test dosyası.

## Acceptance evidence

- `dotnet build --warnaserror` ve testler sıfır uyarı ve hata ile tamamlanır.

## Handoff

- V1-RMD-046
