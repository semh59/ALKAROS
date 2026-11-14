# V1-RMD-1071 - Giriş ekranında ham terminal kodu gösterilmez

- Task ID: V1-RMD-1071
- Status: InProgress
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

Giriş ekranının altında yönetici dışı kullanıcıya "Terminal · 2FD67369" yazıyor; terminal adı oturum açılmadan bilinmediği için bu satır hiçbir zaman ad taşıyamaz. docs/CASHIER_UI_RULES.md 7 ham terminal kodunu kasa başlığında yasaklar. Satır ve ona ait .terminal-reference stil kuralı kaldırılır; terminal adı oturum açıldıktan sonra başlıkta zaten gösterilir.

## Owned surface

- `evidence/V1-RMD-1071/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/PosTerminal/src/routes/Cashier.tsx
  - src/Clients/PosTerminal/src/styles.css
  - src/Clients/PosTerminal/src/routes/Cashier.session-ended-message.test.tsx
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi
3. İstemci üretim derlemesi (oxlint --max-warnings=0, tsc, vite build)

## Out of scope

- Listede olmayan ekranlar ve dosyalar

## Dependencies

- V1-RMD-1070

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. client-build-gate çıktısı exit code 0
3. İki ardışık bağımsız qa-* incelemesi

## Handoff

- None
