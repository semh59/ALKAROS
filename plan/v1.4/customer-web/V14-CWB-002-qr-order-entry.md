# V14-CWB-002 - Build QR order entry

- Task ID: V14-CWB-002
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:II.2.18
- PDF:II.6.8
- PDF:II.7.3
- PDF:III.21

## Goal

QR customer'ın açık final summary ile Order oluşturup PendingConfirmation workflow'una göndermesini sağlamak.

## Owned surface

- `src/Apps/CustomerWeb/OrderEntry/**`, `tests/Apps/CustomerWeb/OrderEntry/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Sepet düzenleme, değiştirici/not girişi, fiyat özeti, yinelenen gönderim koruması ve beklemede-order status.

## Out of scope

- Doğrudan mutfağa gönderim, müşteri payment, personel onayı ve menü yönetimi.

## Dependencies

- V14-CWB-001
- V14-QRO-001
- V14-QRO-002
- V0-CMP-005

## Deliverables

- QR müşteri order giriş arayüzü ve API contract testleri.
- Yinelenen tıklama, son kullanma tarihi, öğe kullanılamıyor, fiyat değişikliği ve table durumu testleri.

## Acceptance evidence

- Submit yalnız tek PendingConfirmation QR Order üretir; approved confirmation öncesinde KitchenTicket oluşturamaz veya
  stok ayıramaz.
- QR order entry, `docs/compliance/accessibility-target.md`'deki customer QR success kriterleri ve approved exception
  kayıtlarını karşılar.

## Handoff

- V14-QRO-003
- V14-OUI-001
