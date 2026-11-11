# V1-RMD-970 - Kasa açılışında önerilen tutar görünür ve ne yapılacağı söylensin

- Task ID: V1-RMD-970
- Status: Done
- Assignee: claude-code-session_019vXeYYn5EiQaDrvjG9cpob
- Work type: implementation
- Surface state: Existing

## Goal

Kasiyer denemesi raporu: açılış ekranında alan önerilen tutarla dolu geliyor ama ipucu tutarı söylemiyor ve öneri yoksa alan 0,00 ile boş bir sayı gibi duruyor; kasiyer ne girmesi gerektiğini anlamıyor. Karar: öneri varsa ipucu 'Önerilen tutar: ₺X (bir önceki kapanıştan). Çekmecedeki parayı sayıp gerekirse değiştirin.' der; öneri yoksa 'Önceki kapanıştan öneri yok; çekmecedeki parayı sayıp yazın.' der.

## Owned surface

- `evidence/V1-RMD-970/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/Cashier/wwwroot/payments/cash-session/cash-session.js
  - tests/Clients/StaticApps/cash-session-load-errors.test.js
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi

## Out of scope

- Listede olmayan ekranlar ve dosyalar

## Dependencies

- V1-RMD-969

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
