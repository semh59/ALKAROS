# V12-PAY-004 - Implement card settlement orchestration

- Task ID: V12-PAY-004
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:I.26-I.29
- PDF:I.49
- PDF:II.2.6
- PDF:II.2.16
- PDF:II.5.3-II.5.4
- PDF:III.8
- PDF:III.19

## Goal

Approved BankCard sonucu, PaymentAllocation ve fiscal request geçişini crash-safe durable workflow ile tamamlamak.

## Owned surface

- `src/Modules/Payments/CardSettlement/**`, `tests/Modules/Payments/CardSettlement/**`,
  `database/migrations/V12/V12-PAY-004/**`
- Bu görev Hugin transport, allocation constraint veya FiscalDocument schema'sını değiştiremez.

## In scope

- Durable state, correlation, approved allocation, fiscal handoff, resume, duplicate suppression ve reconciliation lock.

## Out of scope

- Terminal protocol, refund, cash/meal-card handler ve reconciliation case persistence.

## Dependencies

- V12-HUG-001
- V12-ALC-001
- V12-FSC-001
- V1-FND-005
- V1-FND-006
- V1-SEC-002

## Deliverables

- `src/Modules/Payments/CardSettlement/**` altında durable orchestration production code'u ve migration'ı.
- Provider approval sonrası her crash noktası için failure-injection ve resume testleri.

## Acceptance evidence

- Approved charge allocation olmadan tekrar tahsilata açılamaz; resume aynı provider referansını tek allocation'a
  bağlar.
- Unknown veya allocation/provider mismatch bill'i kapatmaz ve `V12-REC-001` için typed evidence üretir.

## Handoff

- V12-FSC-002
- V12-REC-001
- V12-TBL-001
