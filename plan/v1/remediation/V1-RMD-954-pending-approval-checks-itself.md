# V1-RMD-954 - Onay bekleyen indirim ve ikram kendiliğinden kontrol edilsin

- Task ID: V1-RMD-954
- Status: InProgress
- Assignee: claude-code-session_019vXeYYn5EiQaDrvjG9cpob
- Work type: implementation
- Surface state: Existing

## Goal

Kasiyer denemesi raporu: limit aşan indirim veya ikram yönetici onayına gidince kasiyer 'Onay durumunu kontrol et' düğmesine elle basmak zorunda. Karar: bekleyen istek ekran açıkken birkaç saniyede bir aynı istek aynı anahtarla yeniden gönderilerek kendiliğinden kontrol edilir (form değişmişse yeni istek açılmaz); düğme kalır, metin bunu söyler.

## Owned surface

- `evidence/V1-RMD-954/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.js
  - tests/Clients/StaticApps/split-payment-discount-pending.test.js
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi

## Out of scope

- Listede olmayan ekranlar ve dosyalar

## Dependencies

- V1-RMD-953

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
