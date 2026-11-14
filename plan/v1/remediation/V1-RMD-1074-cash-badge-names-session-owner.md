# V1-RMD-1074 - Başlıktaki Kasa rozeti oturum başkasındaysa sahibinin adını da yazar

- Task ID: V1-RMD-1074
- Status: InProgress
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

V1-RMD-1064 başlıktaki rozeti "Kasa: Başka kullanıcıda" yaptı, fakat sahibin adını yazmadı; ad yalnız kasa oturumu sayfasında (V1-RMD-1063) görünüyor. Kasiyer denemesi 9 başlıkta ad bulamadı. Başkasının oturumunda rozet, mevcut GET .../cash-sessions/active/owner yanıtındaki adı "Kasa: Başka kullanıcıda (Ad)" biçiminde ekler; ad okunamazsa mevcut metin aynen kalır. Yetki kuralı ve sunucu değişmez.

## Owned surface

- `evidence/V1-RMD-1074/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/PosTerminal/src/features/cash-session-status/cashSessionStatusApi.ts
  - src/Clients/PosTerminal/src/features/cash-session-status/CashSessionStatus.tsx
  - src/Clients/PosTerminal/src/features/cash-session-status/CashSessionStatus.test.tsx
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi
3. İstemci üretim derlemesi (oxlint --max-warnings=0, tsc, vite build)

## Out of scope

- Sunucu uç noktası, kasa oturumu sayfası ve listede olmayan dosyalar

## Dependencies

- V1-RMD-1073

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. client-build-gate çıktısı exit code 0
3. İki ardışık bağımsız qa-* incelemesi

## Handoff

- None
