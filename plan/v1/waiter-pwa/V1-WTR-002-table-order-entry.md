# V1-WTR-002 - Implement Waiter PWA table Order entry

- Task ID: V1-WTR-002
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:I.7-I.10

## Goal

Waiter permission kapsamında Table seçimi, Product/modifier/note girişi ve idempotent submit akışını uygulamak.

## Owned surface

- `src/Clients/WaiterPwa/OrderEntry/**`, `tests/Clients/WaiterPwa/OrderEntry/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Table kullanılabilirliği, katalog, draft, sıraya alınmış gönderme, çakışma ve sunucu hatası eşleme.

## Out of scope

- Payment, table yönetimi ve QR müşteri UI'si.

## Dependencies

- V1-WTR-001
- V1-ORD-002
- V1-TBL-001
- V1-CAT-001
- V0-CMP-005

## Deliverables

- `src/Clients/WaiterPwa/OrderEntry/**` altında Goal kapsamını uygulayan production code ve task-specific automated test
  assets.
- Public API/event contract varsa contract testleri dahil otomatik testler.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Yeniden bağlanma/çift gönderme bir Order oluşturur; eski table çakışması görünür durumda ve Order'yi sessizce hareket
  ettirmiyor.
- Order entry, `V0-CMP-005` kararındaki waiter success criteria ve approved exception kayıtlarını karşılar.

## Handoff

- V1-WTR-003
