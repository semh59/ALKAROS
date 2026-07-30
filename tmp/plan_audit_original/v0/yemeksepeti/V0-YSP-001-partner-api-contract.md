# V0-YSP-001 - Validate Yemeksepeti Partner API contract

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: validation

## Goal

Partner erişimi, webhook kimliği, retry, order status, cancellation ve catalog mapping sözleşmesini doğrulamak.

## Owned surface

- `evidence/v0/integrations/V0-YSP-001/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- OAuth/client credentials, webhook security, event identity, status vocabulary, retry count ve market availability.

## Out of scope

- Internal Order mapping veya production webhook handler.

## Dependencies

- V0-ARC-003

## Deliverables

- V0-YSP-001 için tarihli ve kaynakları belirtilmiş evidence package.
- Başarı ve en az bir gerçek hata/edge-case çıktısı.
- Doğrulanamayan maddeler için açık blocker kaydı; varsayımla kapatma yok.

## Acceptance evidence

- Gerçek partner sandbox/portal erişimi ve imzalı ya da tokenlı webhook örneği doğrulanmış.

## Handoff

- V14-ONL-001, V14-ONL-002 ve V14-MAP-002.

