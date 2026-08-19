# V1-BIL-004 - Billing composite integrity and additive migration

- Task ID: V1-BIL-004
- Status: Done
- Assignee: Antigravity-v1-bil-004
- Work type: implementation
- Surface state: Existing

## Goal

Geçmiş migration dosyalarını (`019`, `020`, `021`) geriye dönük değiştirmeden, yeni `032-billing-composite-integrity`
additive migration'ı ile billing composite integrity ve split design kısıtlarını uygulamak; runtime migration
manifesti (`order.json`) ve `ManifestTests` ile doğrulamak.

## Owned surface

- `database/MigrationComposition/order.json`
- `database/migrations/V1/V1-BIL-004/**`
- `src/Modules/Billing/Adjustments/**`
- `src/Modules/Billing/SplitDesign/**`
- `tests/Modules/Billing/Adjustments/**`
- `tests/Modules/Billing/SplitDesign/**`
- `tests/Host/MigrationComposition/Manifest/ManifestTests.cs`
- `evidence/V1-BIL-004/**`

## Dependencies

- V1-BIL-001
- V1-BIL-002
- V1-BIL-003

## Acceptance evidence

- `032-billing-composite-integrity` migration'ı boş PostgreSQL veritabanında UP ve DOWN başarıyla çalışır.
- `dotnet test tests/Host/MigrationComposition/Manifest/ALKAROS.Host.MigrationComposition.Manifest.Tests.csproj` exit 0
  verir.
- `task_scope_tool.py --task-id V1-BIL-004` exit 0 verir.
