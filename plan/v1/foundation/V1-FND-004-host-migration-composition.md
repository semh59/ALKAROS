# V1-FND-004 - Implement host migration composition

- Task ID: V1-FND-004
- Status: Done
- Assignee: opencode-v1-fnd-004
- Work type: implementation
- Surface state: Existing

## Source basis

- PDF:I.7-I.10
- PDF:II.4
- PDF:III.32
- CORR:C1

## Goal

Executable host'u ve module migration'larını doğrulanmış global sırayla çalıştıran tek composition yüzeyini oluşturmak.

## Owned surface

- `src/Host/Composition/Migrations/MigrationCompositionValidator.cs`,
  `src/Host/Composition/Migrations/MigrationDiscoverer.cs`
- `src/Host/Composition/Migrations/MigrationFile.cs`
- `src/Host/Composition/Migrations/MigrationManifest.cs`,
  `src/Host/Composition/Migrations/MigrationManifestException.cs`
- `tests/Host/MigrationComposition/Discovery/MigrationDiscovererTests.cs`
- `tests/Host/MigrationComposition/Fixtures/TestDatabase.cs`
- `tests/Host/MigrationComposition/Fixtures/TestMigrationSet.cs`
- `tests/Host/MigrationComposition/Validation/MigrationCompositionValidatorTests.cs`
- V0-GOV-015 tarafindan remediated host composition, executor ve psql runner
  dosyalari bu task'in yuzeyinden devredilmistir; V0-GOV-015 bu task'a
  dependency ile siralanir.
- Owned surface devri (2026-08-01 kullanıcı onaylı plan değişikliği, V1-FND-002): database/MigrationComposition
  klasöründeki order.json sahipliği V1-FND-002'ye devredildi; database/MigrationComposition klasörü bu yüzeyden
  çıkarıldı.
- Bu görev module-specific schema veya başka bir task'ın migration dosyasını değiştiremez.
- C52 module reachability source and test surface is transferred to V1-FND-017; this historical task remains closed.

## In scope

- Host startup, module registration, migration discovery, kesin sıra, duplicate kimlik reddi ve fail-fast hata sonucu.

## Out of scope

- Domain handler'ları, module-specific schema, seed business data ve production deployment.

## Dependencies

- V1-FND-001
- V1-FND-003
- V0-ARC-001
- V0-DAT-001

## Deliverables

- `src/Host/**` altında executable host ve migration composition production code'u.
- Boş PostgreSQL 18 üzerinde sıra, duplicate, eksik migration ve rollback testleri.

## Acceptance evidence

- Host yalnız kayıtlı module'ları yükler; C1 ile düzeltilen sırayı ihlal eden veya duplicate migration çalıştırılmaz.
- Migration failure non-zero startup sonucu üretir; kısmi başarı gizlenmez.

## Handoff

- V1-FND-005
- V20-MIG-001
