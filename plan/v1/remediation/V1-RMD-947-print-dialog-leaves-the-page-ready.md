# V1-RMD-947 - Fiş yazdırma penceresi açılırken sayfa hazır ve kilitsiz kalsın

- Task ID: V1-RMD-947
- Status: InProgress
- Assignee: claude-code-session_019vXeYYn5EiQaDrvjG9cpob
- Work type: implementation
- Surface state: Existing

## Goal

Kasiyer denemesi raporu (G14): fiş yazdırınca tarayıcının yazdırma penceresi sayfayı kilitledi. Değerlendirme: window.print() tarayıcı tarafında kipli çalışır (kiosk kurulumunda sessiz yazdırma bayrağı bunu kaldırır), uygulama kodunda kilit yoktur. Uygulamanın tek sorumluluğu, yazdırma çağrılırken kendi durumunun kilitli kalmaması ve fiş sayfasının dolu olmasıdır. Karar: bu iki durumu kilitleyen test eklenir; kod değişikliği gerekmiyorsa gerekçe kanıt dosyasına yazılır.

## Owned surface

- `evidence/V1-RMD-947/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - tests/Clients/StaticApps/split-payment-receipt.test.js
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi

## Out of scope

- Listede olmayan ekranlar ve dosyalar

## Dependencies

- V1-RMD-946

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
