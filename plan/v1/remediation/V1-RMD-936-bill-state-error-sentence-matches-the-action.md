# V1-RMD-936 - "Bu hesap durumunda..." hatası yapılan işleme uygun cümleyle gösterilsin

- Task ID: V1-RMD-936
- Status: Done
- Assignee: claude-code-session_019vXeYYn5EiQaDrvjG9cpob
- Work type: implementation
- Surface state: Existing

## Goal

V1-RMD-933 incelemesinden çıkan bulgu: sunucu UNSUPPORTED_BILL_STATE kodunu bölme, indirim, geri alma ve iptal için ayrı cümlelerle gönderiyor, ödeme ekranı ise hepsi için "Bu hesap durumunda bölme tasarımı değiştirilemez." diyor; ödenmiş hesapta indirim denenince yanıltıcı cümle çıkıyor. Karar: bu kod için ekran sunucunun işleme özel Türkçe cümlesini gösterir (sabit bölme cümlesi sözlükten kalkar); geri alma ekranındaki kendi cümlesi aynı kalır. Kilit testi bu kodu çok cümleli kod olarak açıkça işaretler.

## Owned surface

- `evidence/V1-RMD-936/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.js
  - tests/Clients/StaticApps/error-dictionary-lock.test.js
  - tests/Clients/StaticApps/split-payment-discount-pending.test.js
  - tests/Clients/StaticApps/split-payment-undo-adjustment.test.js
  - src/Host/Experience/Billing/BillingSplitApplication.cs
  - tests/Host/Experience/Billing/BillingSplitHttpTests.cs
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test (indirim reddi doğru cümleyi gösterir), düzeltme, mutasyon
2. İki bağımsız qa incelemesi

## Out of scope

- Sunucu cümlelerinin değiştirilmesi, PosTerminal sözlüğü

## Dependencies

- V1-RMD-935

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
