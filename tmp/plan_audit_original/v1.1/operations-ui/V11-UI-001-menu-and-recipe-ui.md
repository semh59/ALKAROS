# V11-UI-001 - Implement menu and recipe administration UI

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Source basis

- Master PDF V1.1 module/schema sections plus named correction dependency.

## Goal

Implement Turkish screens for static/daily menu and immutable recipe version creation/activation.

## Owned surface

- `src/Clients/Cashier/MenuRecipeAdmin/**`, `tests/Clients/Cashier/MenuRecipeAdmin/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Menu composition, daily item setup, ingredient/unit validation, version activation and permission errors.

## Out of scope

- Production execution, stock adjustment and purchasing.

## Dependencies

- V11-MNU-001,V11-MNU-003,V11-RCP-001,V1-IAM-002

## Deliverables

- V11-UI-001 için production implementation.
- Public contract ve otomatik başarı/ret/concurrency testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- UI cannot edit referenced version; unit/dimension error is visible; saved data reloads identically from server.

## Handoff

- V11-UI-002.

