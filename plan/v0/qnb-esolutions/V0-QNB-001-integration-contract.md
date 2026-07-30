# V0-QNB-001 - Validate QNB eSolutions contract

- Task ID: V0-QNB-001
- Status: Blocked
- Assignee: Unassigned (exactly one person)
- Work type: validation
- Surface state: InProgress

## Source basis

- PDF:I.6
- PDF:I.6.3
- CORR:C21
- EXT:QNB-API-PUBLIC

## Goal

Outgoing/incoming e-belge, registered-user query, idempotency, status query ve timeout sözleşmesini doğrulamak.

## Owned surface

- `evidence/v0/integrations/V0-QNB-001/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- API version, authentication, test tenant, document profiles, rate limits, retry ve archive responsibilities.

## Out of scope

- Production invoice generation ve adapter kodu.

## Dependencies

- V0-CMP-001

## Blocker

- Kamuya açık API temel e-belge işlemlerini gösteriyor; test tenant, özel contract ve gerçek document lifecycle
  transcript kanıtı çalışma alanında yoktur.
- Görev ancak test tenant credential/endpoint erişimi ve yürürlükteki private/partner contract sağlandığında `Planned`
  durumuna alınabilir. Gerçek lifecycle transcript'i `Done` acceptance kanıtıdır.

## Deliverables

- V0-QNB-001 için tarihli ve kaynakları belirtilmiş evidence package.
- Başarı ve en az bir gerçek hata/edge-case çıktısı.
- Doğrulanamayan maddeler için açık blocker kaydı; varsayımla kapatma yok.

## Acceptance evidence

- Test tenant üzerinde kayıtlı kullanıcı sorgusu ve en az bir doğrulanmış document lifecycle kanıtı var.

## Handoff

- V13-QNB-001
- V13-QNB-002
- V13-QNB-003
- V13-QNB-004
