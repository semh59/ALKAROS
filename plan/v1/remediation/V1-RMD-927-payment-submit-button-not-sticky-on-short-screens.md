# V1-RMD-927 - 1024x768 ödeme ekranında yapışkan Ödemeyi Ekle düğmesi alanları örtmez

- Task ID: V1-RMD-927
- Status: InProgress
- Assignee: claude-code-session_019vXeYXYQSTiLW1J4UDYBY6
- Work type: implementation
- Surface state: Existing

## Goal

Kasiyer denemesi (G14, bulgu 7): 1024x768'de ödeme sayfasındaki yapışkan (`position: sticky; bottom: 8px`) "Ödemeyi Ekle" düğmesi, görünür alanın altını kapatıp "Alınan para" kutusunu ve 5/10/20/50/100 ₺ hızlı tutar düğmelerini örtüyor; kaydırmadan nakit alınan para girilemiyor. Karar: kısa ekranlarda (yükseklik 860 px ve altı) düğme yapışkan olmaz, formun sonunda normal akışta durur; uzun ekranlarda yapışkan kalır.

## Owned surface

- `evidence/V1-RMD-927/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.css
  - tests/Clients/StaticApps/split-payment-hierarchy.test.js
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi

## Out of scope

- Bu görevin hedefi dışındaki ekranlar

## Dependencies

- V1-RMD-926

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
