# V14-ONL-001 - Implement Yemeksepeti webhook inbox

- Task ID: V14-ONL-001
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:I.34-I.37
- PDF:II.2.19
- PDF:II.7.4
- PDF:III.22

## Goal

Eşzamansız işlemden önce her provider event'nin kimliğini bir kez doğrulayın ve kalıcı hale getirin.

## Owned surface

- `src/Modules/OnlineOrdering/Yemeksepeti/WebhookInbox/**`, `tests/Modules/OnlineOrdering/Yemeksepeti/WebhookInbox/**`,
  `database/migrations/V14/V14-ONL-001/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Webhook belirteç/imza politikası, harici event benzersizliği, ham yük koruması ve retry-güvenli onay.

## Out of scope

- Harici order normalleştirme ve ürün eşleme.

## Dependencies

- V0-YSP-001
- V1-FND-002
- V1-FND-006
- V0-CMP-003
- V1-SEC-001
- V1-SEC-002

## Deliverables

- `src/Modules/OnlineOrdering/Yemeksepeti/WebhookInbox/**` altında Goal kapsamını uygulayan production code ve
  task-specific automated test assets.
- Başarı, ret, replay/race ve güvenlik testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Provider retry, durable insert sonrasında başarılı replay alır; duplicate event tek Inbox
  kaydı üretir; geçersiz kimlik doğrulama hiçbir şeyi saklamaz.

## Handoff

- V14-ONL-002
