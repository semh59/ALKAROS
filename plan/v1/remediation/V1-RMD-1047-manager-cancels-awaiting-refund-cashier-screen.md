# V1-RMD-1047 - Yönetici, "Elle iade bekliyor" iadeyi kasa ekranından sebep göstererek iptal eder

- Task ID: V1-RMD-1047
- Status: InProgress
- Assignee: claude-code-session_019vXeYYn5EiQaDrvjG9cpob
- Work type: implementation
- Surface state: Existing

## Goal

V1-RMD-1045 sunucuda onaylı ama tamamlanmamış iadenin yönetici tarafından zorunlu nedenle iptaline izin verdi. Kasa (split-payment) ekranının iade istekleri listesinde "Elle iade bekliyor" satırı için payments.refund.approve yetkisi olan kullanıcıya gerekçe alanı ve iki adımlı onaylı "Onaylı iadeyi iptal et" düğmesi eklenir; başarıda bildirim gösterilir ve liste yenilenir. Yetkisiz kullanıcı (kasiyer) düğmeyi görmez. Semih kararı 2026-11-13.

## Owned surface

- `evidence/V1-RMD-1047/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.js
  - tests/Clients/StaticApps/split-payment-refunds.test.js
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi

## Out of scope

- Listede olmayan ekranlar ve dosyalar

## Dependencies

- V1-RMD-1046

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
