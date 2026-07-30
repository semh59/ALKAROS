# V15-OBS-001 - Implement structured correlation logging

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Goal

Propagate correlation, request, user/device and provider references through critical flows with redaction.

## Owned surface

- `src/Modules/Observability/StructuredLogging/**`, `tests/Modules/Observability/StructuredLogging/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Context propagation, event naming, severity, sampling boundary and sensitive-field filters.

## Out of scope

- Metrics storage, alert rules and audit event persistence.

## Dependencies

- V15-SEC-003,V1-OPS-001

## Deliverables

- V15-OBS-001 için production implementation veya executable test asset.
- Başarı, ret/failure ve recovery testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- One end-to-end test traces Order to payment/fiscal with one correlation ID and no sensitive plaintext.

## Handoff

- V15-OBS-002 and V20-GAT-002.

