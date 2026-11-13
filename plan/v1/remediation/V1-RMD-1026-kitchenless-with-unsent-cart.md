# V1-RMD-1026 - Gönderilmemiş sepet varken de mutfaksız ekleme seçilebilir

- Task ID: V1-RMD-1026
- Status: Done
- Assignee: claude-code-session_019vXeYYn5EiQaDrvjG9cpob
- Work type: implementation
- Surface state: Existing

## Goal

Satış ekranında masanın daha önce mutfağa gitmiş kalemleri varken yeni gönderilmemiş kalem sepete eklenince Mutfağa göndermeden hesaba ekle düğmesi ve neden seçici hiç çıkmıyordu; kalem silinince çıkıyordu. Düğme ve masa panelinden gelen niyet, siparişte mutfağa gitmiş en az bir kalem varsa gönderilmemiş sepetle de çalışır; hiç gönderilmemiş taslakta gizli kalır.

## Owned surface

- `evidence/V1-RMD-1026/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/PosTerminal/src/routes/Cashier.tsx
  - src/Clients/PosTerminal/src/routes/Cashier.kitchenless.test.tsx
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi

## Out of scope

- Listede olmayan ekranlar ve dosyalar

## Dependencies

- V1-RMD-1025

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
