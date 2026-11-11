# V1-RMD-946 - Cari müşteri kartı sayfa tekrar görününce yenilensin

- Task ID: V1-RMD-946
- Status: Done
- Assignee: claude-code-session_019vXeYYn5EiQaDrvjG9cpob
- Work type: implementation
- Surface state: Existing

## Goal

V1-RMD-945 doğrulamasından: cari müşteri ekranı listeyi ve ekstreyi yalnız açılışta ve kendi işlemlerinden sonra okuyor; ekran açık kalıp borç başka ekranda veya terminalde değişirse kart eski tutarı (ör. "Borç ₺0,00 / Müşterinin borcu yok") gösteriyor. Karar: sayfa tekrar görünür olduğunda (sekme veya uygulama geri gelince) müşteri listesi ve açık müşterinin ekstresi yeniden okunur; yazılmakta olan tutar ve notlar korunur.

## Owned surface

- `evidence/V1-RMD-946/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/Cashier/wwwroot/payments/customer-accounts/customer-accounts.js
  - tests/Clients/StaticApps/customer-accounts-flows.test.js
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi

## Out of scope

- Listede olmayan ekranlar ve dosyalar

## Dependencies

- V1-RMD-945

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
