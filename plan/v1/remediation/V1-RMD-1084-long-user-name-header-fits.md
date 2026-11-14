# V1-RMD-1084 - Uzun kullanıcı adında üst başlık düzeni bozulmaz

- Task ID: V1-RMD-1084
- Status: Planned
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

Kasa kullanıcı denemesi 10: çok uzun adda "Kasa: Başka kullanıcıda (ad)" bağlantısı 2-3 satıra uzar, KULLANICI/ROL bloğu 68 px bara sığmayıp kırpılır; 768 px'te kullanıcı adı görünmez (sıkışık görünümde kimlik öğelerinden ilki, şube yoksa kullanıcı, konum ile gizleniyor). Kasa bağlantısı tek satırda ellipsis ile kısalır, tam metin title ile erişilir; kimlik bloğu sarmaz, şube öğesi konumla değil ad ile gizlenir; 14 px ve 48 px kuralı korunur.

## Owned surface

- `evidence/V1-RMD-1084/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/PosTerminal/src/shell/shell.css
  - src/Clients/PosTerminal/src/shell/ProductionShell.tsx
  - src/Clients/PosTerminal/src/shell/ProductionShell.test.tsx
  - src/Clients/PosTerminal/src/shell/layout-contract.test.ts
  - src/Clients/PosTerminal/src/features/cash-session-status/CashSessionStatus.tsx
  - src/Clients/PosTerminal/src/features/cash-session-status/CashSessionStatus.test.tsx
  - src/Clients/PosTerminal/src/features/cash-session-status/cash-session-status.css
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi
3. İstemci üretim derlemesi (oxlint --max-warnings=0, tsc, vite build)

## Out of scope

- Listede olmayan ekranlar ve dosyalar; arka uç sözleşmesi

## Dependencies

- V1-RMD-1083

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. client-build-gate çıktısı exit code 0
3. İki ardışık bağımsız qa-* incelemesi

## Handoff

- None
