# V11-RSV-002 - Implement atomic last-portion reservation

- Task ID: V11-RSV-002
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:I.21-I.25
- PDF:II.2.12
- PDF:II.3.9
- PDF:II.5.6
- PDF:II.5.14
- PDF:III.14

## Goal

Rakip kanalların aşırı satış yapmaması için satır kilitleme/sürüm kontrolleriyle porsiyonları ayırın.

## Owned surface

- `src/Modules/Inventory/PortionReservations/Concurrency/**`,
  `tests/Modules/Inventory/PortionReservations/Concurrency/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Atomik kullanılabilirlik kontrolü, denge mutasyonu, rezervasyon oluşturma ve kaybetme talebi sonucu.

## Out of scope

- QR/çevrimiçi adapter davranışı ve iptal sınıflandırması.

## Dependencies

- V11-RSV-001
- V11-INV-007

## Deliverables

- `src/Modules/Inventory/PortionReservations/Concurrency/**` altında Goal kapsamını uygulayan production code ve
  task-specific automated test assets.
- Aynı davranışın otomatik başarı, ret ve concurrency/idempotency testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Kalan bir kısım için paralel test, bakiyesi negatif olmayan bir Rezerve ve bir OutOfStock sonucu verir.

## Handoff

- V14-STK-001
