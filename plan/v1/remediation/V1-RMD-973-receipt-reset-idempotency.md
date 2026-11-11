# V1-RMD-973 - Orijinal fiş sıfırlama tekrar anahtarı alır

- Task ID: V1-RMD-973
- Status: InProgress
- Assignee: claude-code-session_019vXeYYn5EiQaDrvjG9cpob
- Work type: implementation
- Surface state: Existing

## Goal

Orijinal fiş sıfırlama isteğinin yanıtı kaybolup istek yeniden gönderilirse hakkın bir kez daha geri verilmesi engellenmeli: istek tekrar anahtarı taşır, aynı anahtarla gelen tekrar ikinci bir sıfırlama yapmaz ve ilk sonucu döndürür.

## Owned surface

- `evidence/V1-RMD-973/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Host/DualScreen/DualScreenApplication.Receipt.cs
  - src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.js
  - tests/Host/Experience/PaymentTender/BillReceiptHttpTests.cs
  - tests/Architecture/ApiConventions/idempotency_allowlist.json
  - tests/Clients/StaticApps/split-payment-receipt-reset.test.js
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi

## Out of scope

- Listede olmayan ekranlar ve dosyalar

## Dependencies

- V1-RMD-972

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
