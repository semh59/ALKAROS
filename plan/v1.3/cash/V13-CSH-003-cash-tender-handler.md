# V13-CSH-003 - Implement cash tender handler

- Task ID: V13-CSH-003
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:I.26-I.29
- PDF:I.49
- PDF:II.2.7
- PDF:II.5.9
- PDF:III.9

## Goal

Cash tender için Payment, PaymentAllocation, CashTransaction ve change sonucunu tek transaction içinde oluşturmak.

## Owned surface

- `src/Modules/Cash/TenderHandler/**`, `tests/Modules/Cash/TenderHandler/**`
- Bu görev CashSession veya allocation persistence schema'sını değiştiremez.

## In scope

- Açık session doğrulaması, tendered amount, change, idempotency ve atomik ledger/allocation yazımı.

## Out of scope

- CashSession lifecycle, drawer hardware, bank-card ve meal-card işlemleri.

## Dependencies

- V13-PAY-002
- V13-CSH-001
- V13-CSH-002
- V13-ALC-001
- V1-FND-005

## Deliverables

- `src/Modules/Cash/TenderHandler/**` altında cash tender production code'u.
- Duplicate, closed-session, insufficient tender, rollback ve concurrent submit testleri.

## Acceptance evidence

- Başarılı komut tam olarak bir Payment, allocation ve CashTransaction üretir; change deterministik hesaplanır.
- Her failure penceresinde dört kaydın tamamı yoktur veya tamamı commit edilmiştir; kısmi cash posting oluşmaz.

## Handoff

- V13-PAY-003
- V13-FSC-002
