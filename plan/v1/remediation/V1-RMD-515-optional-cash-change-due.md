# V1-RMD-515 - Nakit tahsilatta isteğe bağlı para üstü

- Task ID: V1-RMD-515
- Status: InProgress
- Assignee: claude-code-session_01UKcG8ALDsRweQYx6F2bSYe
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-10-13

## Goal

Nakit tahsilatta kasiyerin isteğe bağlı olarak "alınan parayı" girmesi ve para üstünün büyük yazıyla gösterilmesi (PO kararı: isteğe bağlı). Girilmezse akış değişmez; sunucu sözleşmesi (`TenderedAmount`, `changeAmount`) zaten vardır.

## Owned surface

- `plan/v1/remediation/V1-RMD-515-optional-cash-change-due.md`
- `evidence/V1-RMD-515/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.js
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.css
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Clients/StaticApps/split-payment-change.test.js
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Nakit yöntemi seçiliyken "Alınan para (isteğe bağlı)" alanı; tutardan küçükse uyarı ve düğme pasif; girilmişse istek `TenderedAmount` olarak gider; başarıdan sonra "Para üstü: X ₺" büyük yazıyla gösterilir; yeni tahsilat başlayınca temizlenir.

## Out of scope

- Banknot kısayol düğmeleri, sunucu değişikliği.

## Dependencies

- V1-RMD-512

## Acceptance evidence

- Her kabul koşulu için test ve mutasyon kanıtı; çıktılar `evidence/V1-RMD-515/` altındadır.

## Handoff

- None
