# V1-FND-001 - Create the modular monolith skeleton

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Goal

Create the host, module composition boundaries and dependency enforcement required by V0-ARC-001.

## Owned surface

- `src/Host/**`, `src/BuildingBlocks/ModuleComposition/**`, `tests/Architecture/ModuleBoundaries/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Solution projects, module registration contract and automated forbidden-dependency tests.

## Out of scope

- Domain feature handlers, persistence schemas and external adapters.

## Dependencies

- V0 exit gate

## Deliverables

- V1-FND-001 için production implementation.
- Owned surface içinde otomatik başarı, ret ve edge-case testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- The solution builds; architecture tests reject a deliberate forbidden dependency; no feature module contains business behavior.

## Handoff

- V1-FND-002 and every V1 module task.

