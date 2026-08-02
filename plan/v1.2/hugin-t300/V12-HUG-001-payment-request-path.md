# V12-HUG-001 - Implement Hugin T300 payment request path

- Task ID: V12-HUG-001
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:I.26-I.29
- PDF:II.2.16
- PDF:II.3.12
- PDF:II.5.4
- PDF:III.19

## Goal

Doğrulanmış T300 contract'ye karşı onaylanmış ve reddedilen kart payment akışlarını uygulayın.

## Owned surface

- `src/Modules/Payments/Hugin/PaymentRequest/**`, `tests/Modules/Payments/Hugin/PaymentRequest/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Request mapping, correlation, Approved/Declined normalizasyonu ve arındırılmış provider evidence üretimi.

## Out of scope

- Payment/allocation mutasyonu, timeout/bilinmeyen kurtarma ve geri ödeme/iptal.

## Dependencies

- V12-PAY-001
- V12-PAY-002
- V0-HUG-001
- V1-SEC-001
- V1-SEC-002

## Deliverables

- `src/Modules/Payments/Hugin/PaymentRequest/**` altında Goal kapsamını uygulayan production code ve task-specific
  automated test assets.
- Başarı, ret, timeout/retry ve finansal invariant testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Contract testleri artı gerçek sandbox/cihaz transkripti, eşleşen referanslarla birlikte bir onaylanmış ve bir
  reddedilmiş isteği gösterir.
- Bu adapter PaymentAllocation veya Bill status değiştirmez; durable finalization sahibi `V12-PAY-004`dür.

## Handoff

- V12-HUG-002
- V12-PAY-003
- V12-PAY-004
- V12-FSC-002
- V13-ACC-006
