# V13-ACC-003 - Implement CustomerAccount tender posting

- Task ID: V13-ACC-003
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:I.30-I.33
- PDF:II.2.15
- PDF:II.3.11
- PDF:III.18

## Goal

Onaylanmış bir CustomerAccount tender'ını çift kayıt oluşturmadan tek AccountCharge ve PaymentAllocation kaydına
dönüştürmek.

## Owned surface

- `src/Modules/CustomerAccounts/BillCharges/**`, `tests/Modules/CustomerAccounts/BillCharges/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Eligibility, credit-policy sonucu, AccountCharge source, Payment approval ve allocation transaction boundary.

## Out of scope

- Periodic Invoice issuance ve genel credit scoring.

## Dependencies

- V1-FND-005
- V13-ACC-001
- V12-PAY-002
- V12-ALC-001

## Deliverables

- `src/Modules/CustomerAccounts/BillCharges/**` altında Goal kapsamını uygulayan production code ve task-specific
  automated test assets.
- Başarı, ret, retry/idempotency ve veri bütünlüğü testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Retry bir ücret/tahsis üretir; yetersiz politika onayı ne Bill'yi ne de hesabı değiştirmez; miktarlar eşit kalır.

## Handoff

- V13-INV-001
- V13-ACC-008
