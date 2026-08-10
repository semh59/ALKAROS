# V12-ALC-001 - Implement PaymentAllocation persistence constraints

- Task ID: V12-ALC-001
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
- CORR:C4

## Goal

Payment/Bill/segment identity, currency, amount ve idempotency için PaymentAllocation row'larını ve database
enforcement'ı uygulamak.

## Owned surface

- `src/Modules/Payments/Allocations/Persistence/**`, `tests/Modules/Payments/Allocations/Persistence/**`,
  `database/migrations/V12/V12-ALC-001/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Same-bill kısıtlamaları, hedeflenen/doğrudan tahsis benzersizliği, kalan miktar ve değişmez satırlar.

## Out of scope

- Bill status projeksiyonu ve tazminat iadesi.

## Dependencies

- V12-PAY-001
- V1-BIL-002
- V0-DOM-004
- V0-DAT-003

## Deliverables

- `src/Modules/Payments/Allocations/Persistence/**` altında Goal kapsamını uygulayan production code ve task-specific
  automated test assets.
- Başarı, ret, timeout/retry ve finansal invariant testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Çapraz bill, çapraz para birimi, kopya ve aşırı tahsis eklemeleri hem uygulama hem de veritabanı testlerinde başarısız
  olur.
- Sıfır tutarlı allocation kaydı oluşturulamaz.

## Handoff

- V12-ALC-002
- V12-ALC-003
