# V13-QNB-005 - Implement QNB invoice cancellation transport

- Task ID: V13-QNB-005
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:I.30-I.33
- PDF:II.2.17
- PDF:II.5.11
- PDF:III.20
- CORR:C21
- EXT:QNB-API-PUBLIC

## Goal

Yalnız `V0-QNB-001` kanıtında onaylanan QNB iptal/düzeltme işlemini eşlemek ve belirsiz sonuçları sorgulamak.

## Owned surface

- `src/Modules/Invoicing/Qnb/Cancellation/**`, `tests/Modules/Invoicing/Qnb/Cancellation/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Resmi talep/yanıt, idempotency, provider status sorgusu, arındırılmış kanıtlar ve yerel eylem referansı.

## Out of scope

- İptal uygunluğu/muhasebe hesaplaması.

## Dependencies

- V13-INV-004
- V0-QNB-001
- V1-SEC-001
- V1-SEC-002

## Deliverables

- `src/Modules/Invoicing/Qnb/Cancellation/**` altında Goal kapsamını uygulayan production code ve task-specific
  automated test assets.
- Onaylanmış private/partner contract yüzeyi ve otomatik success/failure/idempotency testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- `V0-QNB-001` iptal capability'sini doğrulamazsa görev gerçek assignee ve tarihli kanıtla `NotApplicable` kapanır.
- Capability doğrulanırsa sandbox kanıtları accepted, rejected ve timeout/query sonuçlarını kapsar; retry tek provider
  action üretir.

## Handoff

- V13-QNB-004
