# V1-RMD-525 - Ürün ve adet bazlı kısmi iade (arayüz)

- Task ID: V1-RMD-525
- Status: Planned
- Assignee: claude-code-session_01UKcG8ALDsRweQYx6F2bSYe
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-10-14

## Goal

V1-RMD-524 uçları için kasa sayfası ve PosTerminal iade paneli: kalem ve adet seçimi, iadenin düşeceği ödeme, "stoğa geri dön" seçimi, hesaplanan tutarın gösterimi ve bütçe/onay sonucu mesajları.

## Owned surface

- `plan/v1/remediation/V1-RMD-525-item-refund-ui.md`
- `evidence/V1-RMD-525/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.js
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/billing/RefundPanel.tsx
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/billing/refundApi.ts
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/strings.ts
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- İade yanıtı ve listesi `items` (kalem kimliği, adet, stoğa dön) alanını taşır; kalem adı hesap kalemlerinden eşlenir.
- Tüm metinler Türkçe; hata sabit cümlelerle gösterilir; durumlar (yükleniyor, boş, hata) ele alınır.

## Out of scope

- Sunucu davranışı.

## Dependencies

- V1-RMD-524

## Acceptance evidence

- Her kabul koşulu için test ve mutasyon kanıtı; çıktılar `evidence/V1-RMD-525/` altındadır.

## Handoff

- None
