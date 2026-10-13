# V1-RMD-514 - Hazır iptal ve iade gerekçeleri

- Task ID: V1-RMD-514
- Status: InProgress
- Assignee: claude-code-session_01UKcG8ALDsRweQYx6F2bSYe
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-10-13

## Goal

Hesap iptali, iade isteği ve iade reddi gerekçelerinin hazır listeden tek dokunuşla seçilmesi; listede "Diğer" seçilirse serbest metin açılır (PO kararı: hazır liste + Diğer). Gerekçe sunucuya yine metin olarak gider; sunucu sözleşmesi değişmez.

## Owned surface

- `plan/v1/remediation/V1-RMD-514-preset-cancel-and-refund-reasons.md`
- `evidence/V1-RMD-514/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.js
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Clients/StaticApps/split-payment-reasons.test.js
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Clients/StaticApps/split-payment-refunds.test.js
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Clients/StaticApps/split-payment-failures.test.js
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/billing/RefundPanel.tsx
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/billing/RefundPanel.test.tsx
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/strings.ts
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Ortak liste: Müşteri vazgeçti, Yanlış sipariş, Mutfak hatası, Ürün sorunlu, İkram, Yanlış tutar girildi, Diğer. İptal gerekçesi, iade gerekçesi ve ret gerekçesi için kasa sayfasında ve PosTerminal iade panelinde seçim kutusu; "Diğer" seçilince metin alanı açılır ve boş bırakılamaz.

## Out of scope

- Yönetilebilir (müdürün düzenlediği) liste ve gerekçe bazlı rapor.

## Dependencies

- V1-RMD-512

## Acceptance evidence

- Her kabul koşulu için test ve mutasyon kanıtı; çıktılar `evidence/V1-RMD-514/` altındadır.

## Handoff

- None
