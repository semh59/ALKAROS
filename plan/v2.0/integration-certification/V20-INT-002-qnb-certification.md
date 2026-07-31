# V20-INT-002 - Certify QNB e-invoice integration

- Task ID: V20-INT-002
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: validation
- Surface state: Planned

## Source basis

- PDF:I.32
- CORR:C21
- EXT:QNB-API-PUBLIC

## Goal

Gerçek sandbox yanıtlarını ve mutabakat kanıtlarını kullanarak onaylanmış QNB ortamını ve belge yaşam döngüsünü
onaylayın.

## Owned surface

- `release/evidence/integrations/qnb/**`
- Bu görev QNB adapter kodunu değiştiremez.

## In scope

- Authentication, send, poll, retry, duplicate prevention ve provider/internal status reconciliation.
- Cancellation veya webhook yalnız private/partner contract'ta doğrulanırsa applicable olur.

## Out of scope

- Adapter uygulaması, vergi mükellefi uygulanabilirlik kararı ve Hugin mali akışı.

## Dependencies

- V13-QNB-001
- V13-QNB-002
- V13-QNB-003
- V13-QNB-004
- V13-QNB-005

## Deliverables

- Sandbox sertifika matrisi, redacted request/response transkriptleri ve mutabakat raporu.

## Acceptance evidence

- Public ve private evidence ile applicable olduğu kanıtlanan senaryolar tek traceable sonuç üretir; doğrulanmayan
  cancellation/webhook satırları tarihli `NotApplicable` evidence veya açık blocker taşır.
- `V13-QNB-004` veya `V13-QNB-005` kanıtlı `NotApplicable` ise ilgili reconciliation/cancellation senaryosu sertifika
  matrisine dahil edilmez; kalan senaryolar gerçek sandbox yanıtlarıyla yine doğrulanır.

## Handoff

- V20-GAT-002
