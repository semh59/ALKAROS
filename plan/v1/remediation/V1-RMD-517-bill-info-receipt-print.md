# V1-RMD-517 - Kasa sayfasında bilgi fişi yazdırma

- Task ID: V1-RMD-517
- Status: InProgress
- Assignee: claude-code-session_01UKcG8ALDsRweQYx6F2bSYe
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-10-13

## Goal

Kasa sayfasında (`split-payment`) ödenmiş hesap ekranından "Fişi yazdır" ile mali olmayan bilgi fişinin tarayıcı yazdırma penceresiyle basılması. Fişte "Bu belge mali fiş değildir" ibaresi yer alır.

## Owned surface

- `plan/v1/remediation/V1-RMD-517-bill-info-receipt-print.md`
- `evidence/V1-RMD-517/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.js
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.css
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Clients/StaticApps/split-payment-receipt.test.js
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- "Fişi yazdır" düğmesi (Hesap Ödendi ekranında); fiş verisini V1-RMD-516 ucundan okur; yazdırma çıktısı yalnız fiş alanını gösterir (yazdırma stili); yöntem ve durum etiketleri Türkçe sözlükten; veri okunamazsa sabit Türkçe uyarı ve "Tekrar dene".

## Out of scope

- Mali fiş / yazarkasa (ÖKC) entegrasyonu, termal yazıcı sürücüsü, PosTerminal ekranı.

## Dependencies

- V1-RMD-516

## Acceptance evidence

- Her kabul koşulu için test ve mutasyon kanıtı; çıktılar `evidence/V1-RMD-517/` altındadır.

## Handoff

- None
