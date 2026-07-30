# V13-ACC-008 - Integrate CustomerAccount tender routing

- Task ID: V13-ACC-008
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: integration
- Surface state: Planned

## Source basis

- PDF:I.26-I.33
- PDF:II.2.6
- PDF:II.2.15
- PDF:II.5.3
- PDF:III.8
- CORR:C26

## Goal

V1.2'de fail-closed kalan CustomerAccount tender handler'ını V1.3 composition extension üzerinden kaydetmek ve approved
allocation sonucunu fiscal Bill closure gate'ine bağlamak.

## Owned surface

- `src/Modules/CustomerAccounts/TenderIntegration/**`, `tests/Integration/CustomerAccounts/TenderIntegration/**`
- Bu görev, `V12-PAY-003` veya `V12-FSC-002` owned surface'ini değiştiremez.

## In scope

- Module registration extension, CustomerAccount route enablement, duplicate/missing registration rejection,
  AccountCharge/PaymentAllocation result dispatch ve fiscal closure integration.

## Out of scope

- AccountCharge business rules, generic tender registry, fiscal policy, cash/card account receipt ve invoice issuance.

## Dependencies

- V13-ACC-003
- V12-PAY-002
- V12-PAY-003
- V12-FSC-001
- V12-FSC-002
- V1-FND-002
- V1-FND-005

## Deliverables

- CustomerAccount tender composition extension ve end-to-end routing/fiscal closure integration tests.

## Acceptance evidence

- V1.3 host'ta CustomerAccount request tam olarak bir registered handler'a çözülür; V1.2 host aynı request'i typed
  version-not-enabled sonucu ile reddetmeye devam eder.
- Retry tek AccountCharge, Payment ve PaymentAllocation üretir; CustomerAccount allocation yalnız `V12-FSC-002`
  policy sonucu izin verirse Bill'i final closed yapar.
- Missing/duplicate registration startup'ı fail-closed durdurur; bu task cash/card/meal-card handler'larını değiştirmez.

## Handoff

- V13-UI-001
- V20-UAT-002
