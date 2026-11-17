# V1-RMD-1127 - Sayımı düzelt: önceki girişler dolu gelir

- Task ID: V1-RMD-1127
- Status: Done
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

Kasiyer deneyimi turu 16: Kasa kapanış fark ekranındaki "Sayımı düzelt" kupür girişini sıfırlayıp tek tutar moduna geçiriyor; kasiyer kupürleri baştan girer. Sayım düzeltmeye girince önceki giriş (kupür adetleri ya da tutar, mod ve not) aynen dolu gelir. Kör sayım korunur: sayım ekranında beklenen tutar gösterilmez ve gönderildikten sonra sızmaz (V1-RMD-1121 testi geçer).

## Owned surface

- `evidence/V1-RMD-1127/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/Cashier/wwwroot/payments/cash-session/cash-session.js
  - tests/Clients/StaticApps/cash-session-close-recount.test.js
  - tests/Clients/StaticApps/cash-session-blind-count.test.js
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi
3. İstemci testleri (tests/Clients/StaticApps)

## Out of scope

- Sunucu sözleşmesi; yukarıda sayılmayan ekranlar ve dosyalar

## Dependencies

- V1-RMD-1121

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi
3. tests/Clients/StaticApps testleri exit code 0

## Handoff

- None
