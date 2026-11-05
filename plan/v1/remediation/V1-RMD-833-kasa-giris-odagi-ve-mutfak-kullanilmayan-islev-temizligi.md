# V1-RMD-833 - Kasa giriş alanı odağı gerekçesi ve mutfakta kullanılmayan işlevin silinmesi

- Task ID: V1-RMD-833
- Status: InProgress
- Assignee: claude-code-session_019vXeYXjE3sM6VZKpRMj1L9
- Work type: implementation
- Surface state: Existing

## Goal

V1-RMD-827 doğrulamasında oxlint iki uyarı verdi ve kalıcı uyarı bırakılmaz. (1) `Cashier.tsx` giriş formunda kullanıcı adı alanının `autoFocus` özniteliği bilinçlidir: giriş ekranı tek amaçlı ve kasiyer ilk olarak oraya yazar; uyarı satır içi gerekçeyle bastırılır ve odak davranışı bir testle kilitlenir. (2) `KitchenOperationsWorkspace.tsx` içindeki `nextItemState` hiçbir yerden çağrılmıyor; silinir.

## Owned surface

- `evidence/V1-RMD-833/**`
- `src/Clients/PosTerminal/src/routes/Cashier.login-focus.test.tsx`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/PosTerminal/src/routes/Cashier.tsx (V1-RMD-097) — odak gerekçesi.
  - src/Clients/PosTerminal/src/features/kitchen-operations/KitchenOperationsWorkspace.tsx (V1-RMD-567) — kullanılmayan işlev.
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Giriş ekranı açılınca kullanıcı adı alanına odak gider (test); oxlint 0 uyarı.
2. Mutfak testleri yeşil kalır.

## Out of scope

- Diğer ekranlar.

## Dependencies

- V1-RMD-827

## Acceptance evidence

1. PosTerminal vitest tam paket yeşil, tsc ve oxlint 0 hata 0 uyarı; mutasyon: `autoFocus` kalkarsa test kırılır.
2. Plan audit ve consistency audit 0 hata/0 uyarı; kapsam aracı exit 0.
3. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
