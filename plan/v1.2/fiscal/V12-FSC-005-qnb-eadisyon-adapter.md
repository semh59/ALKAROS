# V12-FSC-005 - Implement selected QNB e-Adisyon adapter

- Task ID: V12-FSC-005
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:II.2.16
- PDF:II.5.4
- EXT:GIB-YNOKC-GUIDE
- EXT:QNB-API-PUBLIC
- CORR:C25

## Goal

Yalnız `V0-CMP-001` QNB e-Adisyon lifecycle'ını seçip `V0-QNB-001` exact private/public contract'ı doğruladığında
open/update/close mapping'ini uygulamak.

## Owned surface

- `src/Modules/Fiscal/AdisyonStrategy/QnbEAdisyon/**`, `tests/Modules/Fiscal/AdisyonStrategy/QnbEAdisyon/**`
- Bu görev, QNB invoice adapter veya ortak composition surface'ini değiştiremez.

## In scope

- Yalnız doğrulanmış endpoint/schema mapping, document correlation, retry/idempotency, sanitized evidence ve typed
  provider failure.

## Out of scope

- Kamuya açık kaynakta bulunmayan capability uydurmak, applicability kararı, T300 adapter ve invoice issuance.

## Dependencies

- GATE-V12-FSC-STRATEGY
- V12-FSC-001
- V0-QNB-001

## Deliverables

- QNB e-Adisyon adapter production code'u ve exact approved contract/sandbox transcript'e bağlı automated tests.

## Acceptance evidence

- QNB branch seçildiyse her command exact approved endpoint/schema ve gerçek sandbox transcript'iyle kanıtlanır; public
  veya private kaynakta bulunmayan davranış uygulanmaz.
- QNB seçilmediyse veya applicability `NotApplicable` ise görev aynı tarihli/onaylı kararla `NotApplicable` olur;
  adapter/stub oluşturulmaz.

## Handoff

- V12-FSC-003
- V20-CMP-001
- V20-INT-002
