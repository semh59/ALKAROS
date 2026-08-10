# V14-QRS-003 - Implement QR customer session lifecycle

- Task ID: V14-QRS-003
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:I.34-I.37
- PDF:II.2.18
- PDF:II.6.8
- PDF:II.7.3
- PDF:III.21

## Goal

Raw Table token'ı reusable browser credential'a çevirmeden QR token validation sonrası revocable customer session
oluşturmak.

## Owned surface

- `src/Modules/QrOrdering/CustomerSession/**`, `tests/Modules/QrOrdering/CustomerSession/**`,
  `database/migrations/V14/V14-QRS-003/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Oturum verme, karma kalıcılığı, boş/mutlak süre sonu, iptal, table bağlama ve çerez/başlık güvenlik politikası.
- Oturumdan token kaynağı ve denetim olayları.

## Out of scope

- QR jeton oluşturma, aktarma aktarımı, menü oluşturma ve order oluşturma.

## Dependencies

- V14-QRS-001
- V14-QRS-002

## Deliverables

- Müşteri oturumu uygulaması ve contract sürümü.
- Sona erme, tekrar oynatma, iptal etme, jeton rotasyonu ve çapraz table izolasyon testleri.
- Kalıcılık gerektiğinde ileri ve geri alma migration.

## Acceptance evidence

- Yakalanan bir ham oturum başka bir table'ye erişemez; iptal edilen, boşta kalma süresi dolan ve mutlak süresi dolan
  oturumlar, denetlenen neden kodlarıyla başarısız olur.

## Handoff

- V14-CWB-001
- V14-CWB-002
