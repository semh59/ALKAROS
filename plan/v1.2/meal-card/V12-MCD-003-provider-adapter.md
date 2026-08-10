# V12-MCD-003 - Implement meal-card adapter SPI and registry

- Task ID: V12-MCD-003
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:I.26-I.29
- PDF:II.2.14
- PDF:II.3.10
- PDF:II.5.10
- PDF:III.17

## Goal

Provider-neutral meal-card adapter SPI, registry ve capability rejection contract'ını oluşturmak.

## Owned surface

- `src/Modules/MealCard/Providers/Registry/**`, `tests/Modules/MealCard/Providers/Registry/**`
- Bu görev provider-specific transport veya başka task'ın owned surface alanını değiştiremez.

## In scope

- Adapter SPI, provider code registry, capability declaration, disabled provider rejection ve composition registration.

## Out of scope

- Provider-specific request/response mapping, credential, sandbox transcript ve CustomerAccount.

## Dependencies

- V0-MCD-001
- V12-MCD-001
- V12-MCD-002
- V12-ALC-003
- V1-SEC-001
- V1-SEC-002

## Deliverables

- Provider-neutral SPI ve registry production code'u.
- Duplicate provider code, disabled provider ve unsupported capability contract testleri.

## Acceptance evidence

- Registry yalnız V0-MCD-001 tarafından onaylanan provider code'larını etkinleştirir; registry içinde provider-specific
  success stub bulunmaz.
- `V0-MCD-001` onaylı provider listesini boş kapatır ve `V12-MCD-001`/`V12-MCD-002` aynı evidence ile
  `NotApplicable` olursa bu task da `NotApplicable` olur; boş registry/adapter oluşturulmaz.

## Handoff

- V12-PAY-003
- V12-REC-001
- V15-REC-001
