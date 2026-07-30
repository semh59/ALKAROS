# V12-PUI-001 - Implement cashier payment and split allocation UI

- Task ID: V12-PUI-001
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:I.26-I.26A

## Goal

Açık Bill tahsisleri üzerine Cash, BankCard ve onaylı MealCard payment kompozisyonunu uygulayın.

## Owned surface

- `src/Clients/Cashier/Payments/SplitPayment/**`, `tests/Clients/Cashier/Payments/SplitPayment/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Eşit/tutar/madde ayrımı, kalan miktar, tender seçimi, bağımsız gönderim ve bilinmeyen durum kilidi.

## Out of scope

- CustomerAccount tender, geri ödeme ve cash kapatma.

## Dependencies

- V12-PAY-002
- V12-PAY-003
- V12-ALC-001
- V12-ALC-002
- V1-BIL-002
- V0-CMP-005

## Deliverables

- `src/Clients/Cashier/Payments/SplitPayment/**` altında Goal kapsamını uygulayan production code ve task-specific
  automated test assets.
- Contract/UI ve otomatik success/failure/retry testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- UI over-allocation gönderemez; Unknown payment duplicate tender'ı engeller; mixed payment yalnız server doğrulamasıyla
  kapanır.
- Payment akışı `V0-CMP-005` kararındaki cashier success criteria ve device/browser matrix'ini karşılar.

## Handoff

- V12-PUI-002
- V12-PUI-003
