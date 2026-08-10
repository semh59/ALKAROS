# V12-PAY-002 - Implement tender command routing

- Task ID: V12-PAY-002
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

Tender request/handler contract'ını tanımlamak; kayıtlı olmayan yöntemleri ve CustomerAccount yöntemini V1.3'e kadar
typed version-not-enabled sonucu ile reddetmek.

## Owned surface

- `src/Modules/Payments/TenderRouting/**`, `tests/Modules/Payments/TenderRouting/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Typed tender request/handler contract'ları, unknown method rejection ve V0-ARC-004 uyumlu version-not-enabled sonucu.

## Out of scope

- Handler registry composition, tender-specific logic, allocation persistence ve CustomerAccount handler implementation.

## Dependencies

- V12-PAY-001
- V0-DAT-002
- V0-ARC-004

## Deliverables

- `src/Modules/Payments/TenderRouting/**` altında Goal kapsamını uygulayan production code ve task-specific automated
  test assets.
- Başarı, ret, timeout/retry ve finansal invariant testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Kayıtlı handler olmadan Cash, BankCard ve MealCard success üretmez; CustomerAccount V1.2'de veri değiştirmeden typed
  version-not-enabled sonucu verir; SplitPayment ve unknown text reddedilir.

## Handoff

- V12-HUG-001
- V12-CSH-001
- V12-MCD-001
- V12-PAY-003
- V13-ACC-003
