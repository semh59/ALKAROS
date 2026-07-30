# V0-YSP-001 - Validate Yemeksepeti Partner API contract

- Task ID: V0-YSP-001
- Status: Blocked
- Assignee: Unassigned (exactly one person)
- Work type: validation
- Surface state: Done

## Source basis

- PDF:I.6
- PDF:I.6.4
- EXT:YSP-PARTNER-2.0.2

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

## Blocker

- Partner API v2.0.2 kamuya açıktır; Partner Portal credential, sandbox ve imzalı veya tokenlı gerçek webhook transcript
  kanıtı çalışma alanında yoktur.
- Görev ancak yürürlükteki Partner contract, Partner Portal credential ve sandbox endpoint erişimi sağlandığında
  `Planned` durumuna alınabilir. Gerçek webhook transcript'i `Done` acceptance kanıtıdır.

## Deliverables

- V0-YSP-001 için tarihli ve kaynakları belirtilmiş evidence package.
- Başarı ve en az bir gerçek hata/edge-case çıktısı.
- Doğrulanamayan maddeler için açık blocker kaydı; varsayımla kapatma yok.

## Acceptance evidence

- Gerçek partner sandbox/portal erişimi ve imzalı ya da tokenlı webhook örneği doğrulanmış.

## Handoff

- V14-ONL-001
- V14-ONL-002
- V14-MAP-002
