# V12-PAY-001 - Implement Payment aggregate

- Task ID: V12-PAY-001
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:I.11-I.15
- PDF:I.26-I.29
- PDF:II.2.6
- PDF:II.3.4-II.3.5
- PDF:II.5.3
- PDF:III.8

## Goal

V0 finansal sözleşmeleri kapsamında payment kimliğini, kanonik status geçişlerini ve para alanlarını uygulayın.

## Owned surface

- `src/Modules/Payments/PaymentAggregate/**`, `tests/Modules/Payments/PaymentAggregate/**`,
  `database/migrations/V12/V12-PAY-001/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Talep edilen, onaylanan, tender edilen, değişiklik ve para birimi değişmezleri; geçiş geçmişi ve satır sürümü.

## Out of scope

- PaymentAllocation, provider aramaları ve geri ödemeler.

## Dependencies

- GATE-V12-ENTRY
- V0-CMP-002

## Deliverables

- `src/Modules/Payments/PaymentAggregate/**` altında Goal kapsamını uygulayan production code ve task-specific automated
  test assets.
- Başarı, ret, timeout/retry ve finansal invariant testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Geçersiz status/para kombinasyonları reddedildi; payment geçmişi değişmez kalır ve para birimi açıktır.
- Sıfır tutarlı tender reddedilir; negatif tutar geçersiz kombinasyon kuralı altında kalır.

## Handoff

- V12-PAY-002
- V12-ALC-001
