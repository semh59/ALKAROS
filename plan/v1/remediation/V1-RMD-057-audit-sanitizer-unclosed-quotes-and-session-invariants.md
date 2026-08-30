# V1-RMD-057 - Audit sanitizer unclosed quotes and session invariants

- Task ID: V1-RMD-057
- Status: Done
- Assignee: 1dec2ab0-b9bc-4bc0-84d5-c4cabf3e4a6a
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-08-29

## Goal

`IAuditSanitizer.cs` içinde kapanış tırnağı bulunmayan bozuk JSON payload'larında (örn: `{"password": "secret123 broken`) secret maskelemesini sağlamak; `BillingSplitStore.cs` içinde masa `current_bill_id` güncellemesini atomik transaction ile güvenceye almak.

## Owned surface

- `plan/v1/remediation/V1-RMD-057-audit-sanitizer-unclosed-quotes-and-session-invariants.md`
- `src/Modules/Audit/EventStore/IAuditSanitizer.cs`
- `tests/Modules/Audit/EventStore/AuditSanitizerTests.cs`
- `src/Host/Experience/Billing/BillingSplitStore.cs`
- `evidence/V1-RMD-057/**`

## In scope

- `FallbackSanitizeText` içindeki regex desenini kapanış tırnağı olmayan veya satır/metin sonuna kadar olan hassas değerleri yakalayacak şekilde güncellemek.
- Kapanış tırnaksız malformed payload için xUnit test senaryosu eklemek.
- `BillingSplitStore.cs` içinde siparişten adisyon türetilirken masa `current_bill_id` kaydını atomik olarak yazmak.

## Out of scope

- Diğer modüllerdeki iş kurallarını değiştirmek.

## Dependencies

- V1-RMD-056

## Deliverables

- Tırnaksız bozuk girdilerde bile secret sızdırmayan `IAuditSanitizer` ve atomik `BillingSplitStore`.

## Acceptance evidence

- `dotnet test tests/Modules/Audit/EventStore/ALKAROS.Audit.EventStore.Tests.csproj` 0 hata verir.

## Handoff

- V1-GOV-033
