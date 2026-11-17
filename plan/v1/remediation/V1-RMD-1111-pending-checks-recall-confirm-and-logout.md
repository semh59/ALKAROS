# V1-RMD-1111 - Masaya geri gönderme tek adım onaylı, Çıkış kullanıcı kartı menüsünde belirgin

- Task ID: V1-RMD-1111
- Status: Done
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

Kasiyer deneyimi turu: bekleyen hesaplarda "Yanlışlıkla gönderildi: masaya geri gönder" düğmesi tahsil et satırının hemen altında onaysız tek dokunuşla siparişi geri alıyor. Düğme ikincil görünür ve ilk dokunuş "Emin misiniz?" teyidi açar; ikinci dokunuş geri gönderir, Vazgeç teyidi kapatır. Kullanıcı kartı menüsünde "Çıkış" düğmesi menünün ilk öğesidir ve ayrı vurgulu görünür; kod doğrulamasında zaten bu durumdaysa değişiklik yapılmaz ve RESULT.md'de yazılır.

## Owned surface

- `evidence/V1-RMD-1111/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/PosTerminal/src/features/pending-checks/PendingChecksWorkspace.tsx
  - src/Clients/PosTerminal/src/features/pending-checks/PendingChecksWorkspace.test.tsx
  - src/Clients/PosTerminal/src/strings.ts
  - src/Clients/PosTerminal/src/routes/Cashier.tsx
  - src/Clients/PosTerminal/src/routes/Cashier.header.test.tsx
  - src/Clients/PosTerminal/src/routes/workspace.test.tsx
  - src/Clients/PosTerminal/src/features/pending-checks/PendingChecksWorkspace.cancel.test.tsx
  - src/Clients/PosTerminal/src/features/pending-checks/pending-checks.css
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi
3. İstemci üretim derlemesi (client-build-gate)

## Out of scope

- Geri gönderme kuralları ve sunucu sözleşmesi; iptal formu

## Dependencies

- V1-RMD-1110

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. client-build-gate çıktısı exit code 0
3. İki ardışık bağımsız qa-* incelemesi

## Handoff

- None
