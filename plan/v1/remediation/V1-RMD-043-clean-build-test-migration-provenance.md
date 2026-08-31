# V1-RMD-043 - Clean build test migration and provenance

- Task ID: V1-RMD-043
- Status: Done
- Assignee: a04804b8-a15d-498a-9753-7b7c3a0e27b3
- Work type: validation
- Surface state: Planned

## Goal

Tüm C#, frontend ve Python testlerini sıfırdan çalıştırmak; 38 migration için boş DB üzerinde `up -> down -> up` döngüsünü doğrulamak; Docker compose ve E2E akışlarını tamamlamak; 34 production assembly için güncel git commit hash provenance'ını üretmek.

## Owned surface

- PO:2026-08-31 kararıyla build/provenance/** yüzeyi V1-RMD-067'ye devredildi; bu historical task closed kalır.
- `evidence/V1-RMD-043/**`

## Dependencies

- V1-RMD-042

## Acceptance evidence

- Tüm .NET derleme ve testleri, Python testleri ve frontend derlemesi sıfır hata ile tamamlanır.
- 38 migration tam döngüde başarıyla çalışır.
- 34 assembly güncel commit hash'ini taşır.

## Handoff

- V1-GOV-025
