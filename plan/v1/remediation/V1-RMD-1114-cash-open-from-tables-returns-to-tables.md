# V1-RMD-1114 - Masalar'dan açılan kasa masalara döner

- Task ID: V1-RMD-1114
- Status: Planned
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

Kasa kullanıcı denemesi 12: üstteki Kasa: Kapalı bağlantısı ve kasa kapalı bandındaki Kasayı aç bağlantısı returnTo taşımıyor; kasa açılınca kasiyer kasa oturumu sayfasında kalıyor. Kasa kapalıyken bu iki bağlantı returnTo=<geçerli sayfa yolu> ekler; kasa oturumu sayfası (V1-RMD-1102) bunu yalnız aynı kaynak içi göreli yol ise kabul eder ve açılınca o sayfaya döner. Kasa sayfası kodu ve sunucu sözleşmesi değişmez; kapalı olmayan durumlarda bağlantı aynı kalır.

## Owned surface

- `evidence/V1-RMD-1114/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/PosTerminal/src/features/cash-session-status/cashSessionStatusApi.ts
  - src/Clients/PosTerminal/src/features/cash-session-status/CashSessionStatus.tsx
  - src/Clients/PosTerminal/src/features/cash-session-status/CashSessionStatus.test.tsx
  - src/Clients/PosTerminal/src/routes/workspace.test.tsx
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi
3. İstemci üretim derlemesi (client-build-gate)

## Out of scope

- Kasa oturumu sayfasındaki returnTo doğrulaması (V1-RMD-1102); kasa kuralları

## Dependencies

- V1-RMD-1113

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. client-build-gate çıktısı exit code 0
3. İki ardışık bağımsız qa-* incelemesi

## Handoff

- None
