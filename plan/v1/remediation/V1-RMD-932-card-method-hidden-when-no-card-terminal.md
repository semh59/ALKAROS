# V1-RMD-932 - Kart terminali tanımlı değilse kart yöntemi görünmesin ve mutabakat kilidi oluşmasın

- Task ID: V1-RMD-932
- Status: Done
- Assignee: claude-code-session_019vXeYYn5EiQaDrvjG9cpob
- Work type: implementation
- Surface state: Existing

## Goal

Kasiyer denemesi (G14, bulgu 4) ve Semih kararı (2026-11-11): kart terminali tanımlı değilken kart denemesi "manuel mutabakat" kaydı açıp hesabı kilitliyor, kasiyer kalanı nakit/EFT ile bitiremiyor. Karar: terminal tanımlı değilse (Token terminal bilgisi kayıtlı değil) kasiyer ödeme ekranında kart yöntemi hiç görünmez ve sunucu kart tahsilatını mutabakat kaydı açmadan reddeder (409 CARD_TERMINAL_NOT_CONFIGURED); tanımlıysa mevcut akış aynen sürer.

## Owned surface

- `evidence/V1-RMD-932/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Host/DualScreen/DualScreenApplication.Payments.cs
  - src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.js
  - tests/Clients/StaticApps/split-payment-display-binding.test.js
  - tests/Host/Experience/PaymentTender/PaymentTenderHttpTests.cs
  - tests/Host/Experience/PaymentTender/TenderRejectionAuditHttpTests.cs
  - tests/Host/Experience/CashSession/CashSessionHttpTests.cs
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı testler: tahsilat özeti kart terminali durumunu bildirir; kart tahsilatı terminal yokken mutabakat kaydı açmadan reddedilir ve ardından EFT çalışır; ödeme ekranı terminal yokken kart düğmesini çizmez
2. Düzeltme, mutasyon
3. İki bağımsız qa incelemesi

## Out of scope

- Gerçek terminal entegrasyonu

## Dependencies

- V1-RMD-931

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
