# V14-ONL-003 - Implement online status and cancellation synchronization

- Task ID: V14-ONL-003
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

Provider status/cancellation değişikliklerini race-safe local transition ile işlemek ve çözümlenemeyen divergence
evidence event'i üretmek.

## Owned surface

- `src/Modules/OnlineOrdering/Yemeksepeti/StatusSync/**`, `tests/Modules/OnlineOrdering/Yemeksepeti/StatusSync/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Outbound idempotency, late cancellation, already-preparing policy, retry ve typed divergence event.

## Out of scope

- Webhook intake, product mapping ve ReconciliationCase oluşturma.

## Dependencies

- V14-ONL-002
- V14-MAP-002
- V0-YSP-001
- V1-SEC-001
- V1-SEC-002
- V11-RSV-003

## Deliverables

- `src/Modules/OnlineOrdering/Yemeksepeti/StatusSync/**` altında Goal kapsamını uygulayan production code ve
  task-specific automated test assets.
- Başarı, ret, replay/race ve güvenlik testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Duplicate status tek etki üretir; cancellation race deterministik kapanır; çözümlenemeyen fark aynı evidence event'i
  idempotent olarak üretir.
- Mutfak başlamadan provider iptali tam olarak bir Release, hazırlık başladıktan sonra tam olarak bir Waste üretir;
  crash/retry stok etkisini tekrarlamaz ve bu görev ReconciliationCase oluşturmaz.

## Handoff

- V14-REC-001
