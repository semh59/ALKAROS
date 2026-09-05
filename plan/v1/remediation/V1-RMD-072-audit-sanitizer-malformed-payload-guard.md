# V1-RMD-072 - Audit sanitizer malformed payload guard and multiline redaction

- Task ID: V1-RMD-072
- Status: Done
- Assignee: claude-code-019uHsMymyuC1tZ5DHtmWsRi
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-08-31

## Goal

`IAuditSanitizer` içinde JSON ayrıştırması başarısız olduğunda devreye giren metin tabanlı yedek redaksiyonu güçlendirmek. Yedek yola düşmeden önce boyut ve biçim doğrulaması eklenir; yedek regex desenleri kaçış karakterli tırnak içeren ve birden fazla satıra yayılan hassas değerleri de maskeler.

## Owned surface

- `plan/v1/remediation/V1-RMD-072-audit-sanitizer-malformed-payload-guard.md`
- `src/Modules/Audit/EventStore/IAuditSanitizer.cs`
- `tests/Modules/Audit/EventStore/AuditSanitizerTests.cs`

## In scope

- `FallbackSanitizeText` çağrılmadan önce ham metnin boyut ve yapı doğrulaması; aşırı büyük veya bariz bozuk girdinin bütün olarak maskelenmesi.
- Yedek regex desenlerinin kaçış karakterli tırnak (`\"`) içeren değerleri kapsaması.
- Yedek regex desenlerinin satır sonu içeren çok satırlı değerleri kapsaması.
- Kaçışlı ve çok satırlı bozuk girdiler için xUnit test senaryoları.

## Out of scope

- Ana `JsonNode` tabanlı ağaç gezme yolunu veya hassas alan listesini değiştirmek.
- Audit modülünün diğer dosyalarını veya depolama davranışını değiştirmek.

## Dependencies

- V1-RMD-071

## Deliverables

- Kaçışlı ve çok satırlı bozuk girdilerde bile hassas değer sızdırmayan `IAuditSanitizer` ve kapsayıcı testler.

## Acceptance evidence

- `dotnet test tests/Modules/Audit/EventStore/ALKAROS.Audit.EventStore.Tests.csproj` sıfır hata verir.
- Semih; parola içeren kaçışlı ve çok satırlı bozuk bir payload örneğinin çıktısında ham parolanın görünmediğini doğrular.

## Handoff

- V1-RMD-073
