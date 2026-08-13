# V1-FND-017 - Independently verify Host data-source bootstrap

- Task ID: V1-FND-017
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: integration
- Surface state: Existing

## Source basis

- CORR:C52

## Goal

Host'un tek doğrulanmış `NpgsqlDataSource` ile constructable olduğunu, modüllerden önce validate edildiğini ve kapanışta
dispose edildiğini bağımsız runtime probe ile doğrulamak.

## Owned surface

- `src/Host/Composition/HostComposition.cs`
- `src/Host/Composition/Modules/ModuleRegistry.cs`
- `tests/Host/MigrationComposition/Composition/HostConstructabilityTests.cs`
- `tests/Host/MigrationComposition/Composition/HostServiceRegistrationTests.cs`
- `tests/Host/MigrationComposition/Registry/ModuleRegistryTests.cs`
- `tests/Host/MigrationComposition/Composition/HostModuleReachabilityTests.cs`
- `evidence/V1-FND-017/**`

## In scope

- `CODE-001;CODE-002` için explicit executable module catalog ve tek validated `NpgsqlDataSource` composition akışını
  uygulamak.
- Module discovery, data-source disposal ve missing-module startup failure'ını aynı Host integration test setiyle
  doğrulamak.

## Out of scope

- Owned surface dışındaki Host, migration, project, lock veya plan dosyası değiştirmek.

## Dependencies

- V0-GOV-035
- V1-FND-013
- V1-SEC-003

## Deliverables

- Tek Host integration diff'i, focused runtime tests ve `evidence/V1-FND-017/**` altında raw transcript.

## Acceptance evidence

- Default discovery implemented modules'ın exact setini yükler; beklenen module eksikse startup fail-closed olur.
- NpgsqlDataSource module registration'dan önce validate edilir ve provider dispose edilir.
- İlgili Host testleri ve `python -B tools/plan-audit/plan_audit_tool.py validate` exit code `0` verir.

## Handoff

- V0-GOV-045
