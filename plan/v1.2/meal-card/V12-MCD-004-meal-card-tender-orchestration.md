# V12-MCD-004 - Implement meal-card tender orchestration

- Task ID: V12-MCD-004
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:I.6.2
- PDF:I.26-I.29
- PDF:II.2.6
- PDF:II.5.3
- PDF:III.8
- CORR:C24

## Goal

Approved meal-card provider sonucunu tek PaymentAllocation ve fiscal handoff'a provider-neutral durable workflow ile
bağlamak.

## Owned surface

- `src/Modules/Payments/MealCardSettlement/**`, `tests/Modules/Payments/MealCardSettlement/**`,
  `database/migrations/V12/V12-MCD-004/**`
- Bu görev, provider adapter veya ortak registry surface'ini değiştiremez.

## In scope

- Durable state, provider correlation, Approved/Declined/Unknown mapping, allocation, fiscal handoff, resume, duplicate
  suppression ve typed divergence evidence.

## Out of scope

- Provider protocol mapping, provider settlement grouping, ReconciliationCase persistence ve Bill final close writer.

## Dependencies

- GATE-V12-MEAL-CARD-ADAPTERS
- V12-MCD-001
- V12-MCD-003
- V12-ALC-001
- V12-FSC-001
- V1-FND-005
- V1-FND-006
- V1-SEC-002

## Deliverables

- Provider-neutral meal-card tender workflow'u, migration ve her crash noktası için resume/idempotency tests.

## Acceptance evidence

- Approved provider reference tam olarak bir Payment ve bir PaymentAllocation'a bağlanır; crash/retry ikinci provider
  debit veya allocation oluşturmaz ve fiscal handoff kaybolmaz.
- Declined allocation üretmez; Unknown Bill'i kapatmaz ve `V12-REC-001` için aynı typed divergence evidence'ini
  idempotent üretir.
- `V0-MCD-001` tarihli sonucu meal-card kapsamını `NotApplicable` yaparsa bu görev de aynı evidence ile
  `NotApplicable` olur; success stub veya boş adapter oluşturulmaz.
- Bu durumda `V12-MCD-001` ve `V12-MCD-003` sonuçları da aynı decision evidence'ına bağlı `NotApplicable` olmalıdır;
  dependency zinciri varsayımsal kod üreterek kapatılamaz.

## Handoff

- V12-PAY-003
- V12-FSC-002
- V12-REC-001
