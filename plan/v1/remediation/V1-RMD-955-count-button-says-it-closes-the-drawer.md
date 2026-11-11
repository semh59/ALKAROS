# V1-RMD-955 - Sayım düğmesi kasayı kapatmanın başladığını söylesin

- Task ID: V1-RMD-955
- Status: InProgress
- Assignee: claude-code-session_019vXeYYn5EiQaDrvjG9cpob
- Work type: implementation
- Surface state: Existing

## Goal

Kasiyer denemesi raporu: Kasa oturumu ekranındaki 'Sayıma Başla' düğmesi kasiyere bunun vardiya kapanışının ilk adımı olduğunu söylemiyor. Karar: düğme 'Kasayı Kapat: Sayıma Başla' yazar ve altında tek cümle sayımın kasa kapanışının başlangıcı olduğunu söyler.

## Owned surface

- `evidence/V1-RMD-955/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/Cashier/wwwroot/payments/cash-session/cash-session.js
  - tests/Clients/StaticApps/cash-session-movement.test.js
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi

## Out of scope

- Listede olmayan ekranlar ve dosyalar

## Dependencies

- V1-RMD-954

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
