# V12-ALC-002 - Implement Bill allocation and payment-satisfied projections

- Task ID: V12-ALC-002
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:I.26-I.29
- PDF:II.2.6
- PDF:II.3.4-II.3.5
- PDF:II.5.3
- PDF:III.8

## Goal

Allocated, paid ve change total değerlerini hesaplamak ve PaymentSatisfied projection'ını authoritative Payment
kayıtlarından atomik üretmek.

## Owned surface

- `src/Modules/Billing/PaymentClosure/**`, `tests/Modules/Billing/PaymentClosure/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Projeksiyon formülleri, rebuild, kısmen tahsis edilmiş/ücretli durumlar ve kapatma engelleyicileri.

## Out of scope

- Ödemeler oluşturma, final Bill close status, fiscal gate ve geri ödeme provider çağrıları.

## Dependencies

- V12-ALC-001
- V1-BIL-001
- V0-DAT-004
- V1-FND-005

## Deliverables

- `src/Modules/Billing/PaymentClosure/**` altında Goal kapsamını uygulayan production code ve task-specific automated
  test assets.
- Başarı, ret, timeout/retry ve finansal invariant testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Rebuild projection deterministiktir; Pending/Unknown/Declined/Cancelled payment, PaymentSatisfied üretemez.
- Kesin allocation toplamı PaymentSatisfied değerini bir kez üretir; `V12-FSC-002` kararı olmadan Bill final closed
  status'e geçmez.

## Handoff

- V12-FSC-002
- V12-REC-001
