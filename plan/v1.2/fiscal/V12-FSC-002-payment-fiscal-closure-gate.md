# V12-FSC-002 - Implement payment-fiscal closure gate

- Task ID: V12-FSC-002
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

Fiscal kapsamındaki bir Bill'in ne zaman close edilebileceğine veya reconciliation gerektirdiğine onaylı legal/device
policy ile karar vermek.

## Owned surface

- `src/Modules/Fiscal/PaymentClosureGate/**`, `tests/Modules/Fiscal/PaymentClosureGate/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Gerekli belge stratejisi, yayınlanan/bekleyen/reddedilen/bilinmeyen sonuçlar ve atomik Bill geçişi.

## Out of scope

- Mali belge aktarımı uygulama ayrıntıları.

## Dependencies

- V12-ALC-002
- V12-HUG-001
- V12-FSC-001
- V12-FSC-003
- V12-PAY-004
- V12-ALC-004
- V0-CMP-001
- V1-FND-005
- V12-CSH-003
- V12-MCD-004

## Deliverables

- `src/Modules/Fiscal/PaymentClosureGate/**` altında Goal kapsamını uygulayan production code ve task-specific automated
  test assets.
- Başarı, ret, timeout/retry ve finansal invariant testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Bill kapatma matrisi cash/kart ve mali sonuçları kapsar; engelleme koşulları doğrudan status güncellemesiyle
  atlanamaz.
- Cash, BankCard ve MealCard PaymentSatisfied sonuçları aynı fiscal policy gate'inden geçer; başka hiçbir task final
  Bill closed status yazmaz.
- `V12-MCD-004` tarihli `NotApplicable` ise MealCard branch disabled kalır; Cash ve BankCard closure matrisi yine
  doğrulanır ve bu task başlayabilir.
- Approved-without-allocation, refund Unknown ve fiscal/payment mismatch Bill'i kapatmaz.

## Handoff

- V12-REC-001
- V13-ACC-008
