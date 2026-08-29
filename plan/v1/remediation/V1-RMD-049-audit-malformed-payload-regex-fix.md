# V1-RMD-049 - Audit malformed payload regex fix

- Task ID: V1-RMD-049
- Status: InProgress
- Assignee: a04804b8-a15d-498a-9753-7b7c3a0e27b3
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-08-29

## Goal

`IAuditSanitizer.cs` içindeki `FallbackSanitizeText` regex fonksiyonunu malformed/bozuk JSON içinde tırnaklı anahtarları ve değerleri (ör. `{"password": "secret123" broken`) güvenli şekilde yakalayıp redakte edecek şekilde güncellemek ve kapsamlı xUnit güvenlik testlerini yazmak.

## Owned surface

- `plan/v1/remediation/V1-RMD-049-audit-malformed-payload-regex-fix.md`
- `src/Modules/Audit/EventStore/IAuditSanitizer.cs`
- `tests/Modules/Audit/EventStore/AuditSanitizerTests.cs`
- `evidence/V1-RMD-049/**`

## In scope

- `FallbackSanitizeText` içinde tırnaklı/tırnaksız anahtar ve değerleri redakte eden güvenli regex desenlerini uygulamak.
- `AuditSanitizerTests.cs` içine malformed JSON, tırnaklı gizli anahtarlar ve uç durum testlerini eklemek (CA1707 uyumlu PascalCase isimlendirme).

## Out of scope

- Diğer modülleri değiştirmek.

## Dependencies

- V1-GOV-028

## Deliverables

- Güvenli audit sanitizer ve kapsamlı testleri.

## Acceptance evidence

- Bozuk JSON girdilerinde (`{"password": "secret123" broken`) hassas verilerin `[REDACTED]` ile maskelendiği xUnit testleri ile kanıtlanır.

## Handoff

- V1-RMD-050
