# V0-DOM-005 - Define table reservation policy

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: decision

## Source basis

- PDF baseline plus the correction/gap named in this task; unsupported behavior requires business or external evidence.

## Goal

Define what Table.Reserved means, who can create it, expiry behavior and how walk-in, staff seating and QR interact.

## Owned surface

- `docs/domain/table-reservation-policy.md`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Reservation identity/actor/time, occupancy precedence, expiry, cancellation and concurrent state rules.

## Out of scope

- Booking/reservation UI or implementation.

## Dependencies

- V0-DOM-001

## Deliverables

- V0-DOM-005 için bağlayıcı karar dokümanı.
- Pozitif/negatif örnekler ve rejected alternatives.
- Tüketici görevler için test edilebilir invariant/output listesi.

## Acceptance evidence

- Reserved state has a persisted owner/reason/expiry model or is explicitly removed; QR cannot invent a reservation semantics.

## Handoff

- V1-TBL-004 and V14-QRO-002.

