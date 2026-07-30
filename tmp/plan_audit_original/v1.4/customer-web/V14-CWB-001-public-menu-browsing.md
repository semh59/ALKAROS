# V14-CWB-001 - Build public menu browsing

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Goal

Render the currently sellable menu for an authenticated QR customer session without exposing internal administration data.

## Owned surface

- `src/Apps/CustomerWeb/Menu/**`, `tests/Apps/CustomerWeb/Menu/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Category navigation, item details, price/allergen/availability presentation and stale-session handling.

## Out of scope

- Cart submission, payment, QR token issuance and menu administration.

## Dependencies

- V14-QRS-003, V11-MNU-003, V14-STK-001

## Deliverables

- Responsive public menu interface.
- Accessibility, authorization, stale-data and unavailable-item tests.

## Acceptance evidence

- A valid table session sees only published sellable items; revoked/expired sessions and unavailable products are handled without leaking internal identifiers.

## Handoff

- V14-CWB-002.
