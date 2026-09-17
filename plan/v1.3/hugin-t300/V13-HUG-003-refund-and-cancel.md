# V13-HUG-003 - Implement Token/Beko refund and cancellation transport

- Task ID: V13-HUG-003
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
- CORR:C98

## Goal

**Retarget notu (2026-09-17, `V0-GOV-064`/CORR:C98):** hedef cihaz Hugin
T300 değil, Token/Beko (300 TR / X30 TR). Task ID değişmedi.

Onaylı RefundIntent için iptal/iade işlemini gönderip Approved, Rejected veya Unknown provider sonucunu kaydetmek.

## Owned surface

- `src/Modules/Payments/Token/RefundTransport/**`, `tests/Modules/Payments/Token/RefundTransport/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Intent mapping, tutar/referans, Approved/Rejected/Unknown normalization, timeout query ve idempotent provider action.

## Out of scope

- Compensating allocation, net-paid mutation, fiscal document ve reconciliation case oluşturma.

## Dependencies

- V13-HUG-001
- V13-HUG-002
- V13-ALC-003
- V0-HUG-001
- V1-SEC-001
- V1-SEC-002

## Deliverables

- `src/Modules/Payments/Token/RefundTransport/**` altında Goal kapsamını uygulayan production code ve task-specific
  automated test assets.
- Başarı, ret, timeout/retry ve finansal invariant testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Gerçek sandbox/device kanıtı, başarılı geri ödemeyi ve timeout sorgusunu kapsar; tekrarlanan istek bir terminal işlemi
  üretir.

## Handoff

- V13-ALC-004
