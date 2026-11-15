# V1-RMD-1101 - Ödeme bitince birincil eylem sıradaki işi söyler

- Task ID: V1-RMD-1101
- Status: Done
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

Kasiyer deneyimi turu (kasa kolaylığı): "Hesap Ödendi" ekranında birincil düğme "Fişi yazdır" ve tek yön "Bekleyen hesaplara dön"; kasiyer sıradaki işe kendisi yol arıyor. Ödeme sayfası hesabın masa mı hızlı satış mı olduğunu zaten bilir. Hesap ödendi ekranına yönlendirme düğmesi eklenir: masa hesabıysa birincil "Masalara dön" (/tables), hızlı satış hesabıysa birincil "Yeni satış" (/); "Bekleyen hesaplara dön" ikincil kalır; fiş yazdırma bu ekranda ikincil görünür. Sunucu sözleşmesi değişmez.

## Owned surface

- `evidence/V1-RMD-1101/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.js
  - tests/Clients/StaticApps/split-payment-next-step.test.js
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi
3. İstemci üretim derlemesi (client-build-gate)

## Out of scope

- Sunucu sözleşmesi; iptal ekranı; fiş içeriği

## Dependencies

- V1-RMD-1100

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. client-build-gate çıktısı exit code 0
3. İki ardışık bağımsız qa-* incelemesi

## Handoff

- None
