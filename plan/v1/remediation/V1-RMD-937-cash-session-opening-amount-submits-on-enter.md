# V1-RMD-937 - Kasa açılışında tutar alanında Enter tuşu kasayı açsın

- Task ID: V1-RMD-937
- Status: Done
- Assignee: claude-code-session_019vXeYYn5EiQaDrvjG9cpob
- Work type: implementation
- Surface state: Existing

## Goal

Kasiyer denemesi raporu (G14): kasa açılış ekranında açılış tutarı yazılıp Enter'a basılınca hiçbir şey olmuyor, yalnız "Kasayı Aç" düğmesi çalışıyor. Karar: tutar alanında Enter, düğmeyle aynı doğrulama ve gönderim yolunu çalıştırır.

## Owned surface

- `evidence/V1-RMD-937/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/Cashier/wwwroot/payments/cash-session/cash-session.js
  - tests/Clients/StaticApps/cash-session-load-errors.test.js
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi

## Out of scope

- Diğer ekranların Enter davranışı

## Dependencies

- V1-RMD-936

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
