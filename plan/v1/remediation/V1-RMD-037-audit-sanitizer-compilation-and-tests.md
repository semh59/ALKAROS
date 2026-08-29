# V1-RMD-037 - Audit sanitizer compilation and tests

- Task ID: V1-RMD-037
- Status: Planned
- Assignee: Unassigned
- Work type: implementation
- Surface state: Planned

## Goal

`IAuditSanitizer.cs` içindeki eksik süslü parantez (CS1513) derleme hatasını düzeltmek; malformed JSON, credential redaction ve geçerli JSONB serileştirme testlerini ekleyerek C# katmanının hatasız derlenmesini sağlamak.

## Owned surface

- `src/Modules/Audit/EventStore/IAuditSanitizer.cs`
- `tests/Modules/Audit/EventStore/AuditSanitizerTests.cs`
- `evidence/V1-RMD-037/**`

## Dependencies

- V1-GOV-024

## Acceptance evidence

- `IAuditSanitizer.cs` sözdizimi geçerlidir; `FallbackSanitizeText` ve `SerializeAndSanitize` metotları ayrık bloklardadır.
- AuditSanitizer birim testleri malformed JSON, credential gizleme ve geçerli JSON durumlarını doğrular.

## Handoff

- V1-CUI-005
- V1-WTR-007
- V1-RMD-039
- V1-RMD-040
