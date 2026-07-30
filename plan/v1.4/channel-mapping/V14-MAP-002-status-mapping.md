# V14-MAP-002 - Implement provider status mapping

- Task ID: V14-MAP-002
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:I.34-I.37
- PDF:II.2.19
- PDF:II.7.4
- PDF:III.22

## Goal

Doğrulanan her Yemeksepeti status'ünü izinli internal command, explicit no-op veya typed unknown-status evidence
sonucuna eşlemek.

## Owned surface

- `src/Modules/OnlineOrdering/Yemeksepeti/StatusMapping/**`, `tests/Modules/OnlineOrdering/Yemeksepeti/StatusMapping/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Provider vocabulary version, integration-kind differences, cancellation reason ve unknown status evidence.

## Out of scope

- Webhook authentication, transport retry ve ReconciliationCase oluşturma.

## Dependencies

- V0-YSP-001
- V0-DOM-001

## Deliverables

- `src/Modules/OnlineOrdering/Yemeksepeti/StatusMapping/**` altında Goal kapsamını uygulayan production code ve
  task-specific automated test assets.
- Başarı, ret, replay/race ve güvenlik testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Belgelenen her provider status'ünün tek sonucu vardır; unknown status Order'ı değiştirmez ve idempotent evidence event
  üretir.

## Handoff

- V14-ONL-003
