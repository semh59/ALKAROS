# V12-PAY-003 - Compose tender handlers

- Task ID: V12-PAY-003
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: integration
- Surface state: Planned

## Source basis

- PDF:I.26-I.29
- PDF:II.2.6
- PDF:II.5.3
- PDF:III.8

## Goal

Cash handler, durable BankCard workflow ve MealCard provider-registry bridge'ini tek fail-closed registry'de kaydetmek.

## Owned surface

- `src/Modules/Payments/TenderComposition/**`, `tests/Modules/Payments/TenderComposition/**`
- Bu görev handler business logic'i veya provider adapter kodu yazamaz.

## In scope

- Composition registration, duplicate method rejection, disabled capability ve CustomerAccount version routing.

## Out of scope

- Tender işlemi, allocation persistence, provider transport ve CustomerAccount posting.

## Dependencies

- V12-PAY-002
- V12-CSH-003
- V12-PAY-004
- V12-MCD-004

## Deliverables

- `src/Modules/Payments/TenderComposition/**` altında registry composition production code'u.
- Missing, duplicate, disabled ve unsupported method contract testleri.

## Acceptance evidence

- Yalnız tamamlanmış Cash ve BankCard workflow kaydı çözümlenir; missing veya duplicate kayıt startup'ı başarısız yapar.
- MealCard yalnız gerçek provider adapter ve `V12-MCD-004` durable workflow kaydı varsa enable edilir; aksi halde veri
  değiştirmeden typed unavailable sonucu verir. V1.2 CustomerAccount da unavailable kalır; success stub yoktur.
- `V12-MCD-004` tarihli `NotApplicable` ise MealCard registry'ye girmez; Cash ve BankCard handler registration
  doğrulaması bağımsız devam eder.

## Handoff

- V12-PUI-001
- V13-ACC-003
- V13-ACC-008
