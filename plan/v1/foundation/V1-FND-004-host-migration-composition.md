# V1-FND-004 - Implement host migration composition

- Task ID: V1-FND-004
- Status: Done
- Assignee: opencode-v1-fnd-004
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:I.7-I.10
- PDF:II.4
- PDF:III.32
- CORR:C1

## Goal

Executable host'u ve module migration'larını doğrulanmış global sırayla çalıştıran tek composition yüzeyini oluşturmak.

## Owned surface

- `src/Host/Program.cs`, `src/Host/Composition/**`, `tests/Host/MigrationComposition/**`
- `database/MigrationComposition/**`
- Bu görev module-specific schema veya başka bir task'ın migration dosyasını değiştiremez.

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
