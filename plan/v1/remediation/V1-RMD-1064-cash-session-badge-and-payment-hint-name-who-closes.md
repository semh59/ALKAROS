# V1-RMD-1064 - Başlıktaki Kasa rozeti oturum başkasınındaysa bunu söyler ve tahsilat sayfası kimin kapatacağını yazar

- Task ID: V1-RMD-1064
- Status: InProgress
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

Satış ve masa ekranlarının başlığındaki "Kasa: Açık" rozeti, açık oturum giriş yapan kasiyere ait olmadığında da "Açık" yazıyor; kasiyer nakit alamayınca nedenini göremiyor. Oturum başkasınınsa rozet "Kasa: Başka kullanıcıda" yazar ve çalışma alanının üstünde kalıcı bir şerit "oturumu açan kasiyer ya da bir yönetici kapatmalı" der (kapalı durumundaki şerit gibi, kasa oturumu sayfasına bağlantıyla). Tahsilat sayfasındaki "Önce kasayı açın" açıklaması da yanlış olarak yalnız açanın kapatabileceğini söylüyor; "açan kasiyer ya da bir yönetici" olarak düzeltilir. Yetki kuralı değişmez.

## Owned surface

- `evidence/V1-RMD-1064/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/PosTerminal/src/features/cash-session-status/cashSessionStatusApi.ts
  - src/Clients/PosTerminal/src/features/cash-session-status/CashSessionStatus.tsx
  - src/Clients/PosTerminal/src/features/cash-session-status/CashSessionStatus.test.tsx
  - src/Clients/PosTerminal/src/routes/Cashier.tsx
  - src/Clients/PosTerminal/src/strings.ts
  - src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.js
  - tests/Clients/StaticApps/split-payment-cash-state.test.js
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi
3. İstemci üretim derlemesi (oxlint --max-warnings=0, tsc, vite build) ve tests/Clients/StaticApps tam koşusu

## Out of scope

- Listede olmayan ekranlar ve dosyalar; kasa oturumu sayfasının kendisi (V1-RMD-1063)

## Dependencies

- V1-RMD-1063

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. client-build-gate çıktısı exit code 0
3. İki ardışık bağımsız qa-* incelemesi

## Handoff

- None
