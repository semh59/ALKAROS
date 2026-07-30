# V0-HUG-001 - Validate Hugin T300 integration contract

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: validation

## Goal

T300 payment, fiscal, timeout, unknown, cancellation, refund ve reconciliation sözleşmesini gerçek doküman ve erişimle doğrulamak.

## Owned surface

- `evidence/v0/integrations/V0-HUG-001/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Protocol/SDK version, topology, credentials, test device, request IDs, error codes ve recovery operations.

## Out of scope

- Production Hugin adapter implementasyonu.

## Dependencies

- V0-CMP-001,V0-DOM-003

## Deliverables

- V0-HUG-001 için tarihli ve kaynakları belirtilmiş evidence package.
- Başarı ve en az bir gerçek hata/edge-case çıktısı.
- Doğrulanamayan maddeler için açık blocker kaydı; varsayımla kapatma yok.

## Acceptance evidence

- Gerçek test cihazı veya resmi sandbox üzerinde success, decline, timeout ve query/reconcile kanıtı var.

## Handoff

- V12-HUG-001, V12-HUG-002 ve V12-HUG-003.

