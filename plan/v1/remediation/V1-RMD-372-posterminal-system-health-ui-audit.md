# V1-RMD-372 - PosTerminal Sistem Sağlığı modül denetimi: axe taraması eksikliği + yanlış hata sınıfı

- Task ID: V1-RMD-372
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-27

## Goal

Modül-modül arayüz denetim sürecinin (plan/v1/ui-audit/UI_AUDIT_PROGRESS.md) 12. modülü:
PosTerminal'in Sistem Sağlığı ekranı (`src/Clients/PosTerminal/src/features/system-health/**`,
~110 satır — bu oturumun en küçük modülü) ve onu bağlayan `routes/workspace.tsx`'in
`SystemHealthRoute` parçası. On iki boyut üzerinden tarandı.

İki bulgu, ikisi de daha önceki modüllerde zaten tanımlanmış desenlerin tekrarı:

1. **[Modül 5/10 sınıfı] `SystemHealthWorkspace.tsx`'in hiçbir axe-core taraması yoktu.**
2. **[Modül 7/8/9 sınıfı] `workspace.tsx`'in `SystemHealthRoute.load()`'u yalnızca `reason
   instanceof ApiError` kontrol ediyordu**, ama bu rota kendi API'sine sahip değil —
   `kitchen-operations`'ın istemcisini yeniden kullanıyor (`createKitchenOperationsClient`),
   dolayısıyla gerçek hataları `KitchenOperationsApiError`. V1-RMD-369'da (Modül 9) bilinçli
   olarak bu modüle ertelenmişti — şimdi düzeltildi.

## Owned surface

- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/routes/workspace.tsx
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/routes/workspace.test.tsx
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/system-health/SystemHealthWorkspace.test.tsx
- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/v1/ui-audit/UI_AUDIT_PROGRESS.md
- `plan/v1/remediation/V1-RMD-372-posterminal-system-health-ui-audit.md`

## In scope

1. **[T1, Yüksek — test kapsamı] `SystemHealthWorkspace.tsx` hiçbir axe-core taramasından
   geçmiyordu.** Aynı standart desenle yeni bir test eklendi.
2. **[T3/T6, Yüksek — hata yönetimi/frontend-backend uyumu] `SystemHealthRoute.load()`
   yalnızca `reason instanceof ApiError` kontrol ediyordu.** `reason instanceof ApiError ||
   reason instanceof KitchenOperationsApiError` şeklinde düzeltildi.

## Out of scope

- Ürün-katmanı (P1-P4) gözlemi yok; salt-okunur bir gösterge paneli, karmaşıklık yok.

## Dependencies

- None

## Acceptance evidence

- `src/Clients/PosTerminal`: `npx tsc --noEmit` sıfır hata; `npx vitest run` tam paketi (37
  dosya, 278 test, bu görevin 2 yeni testi dahil): 278/278 geçti, regresyon yok.
- Mutation-check: `workspace.tsx` `git stash` ile geri alındı, yeni test GERÇEKTEN kırmızı oldu
  (jenerik yedek metin gösterildi). `git stash pop` ile geri yüklendi, paket tekrar 278/278
  yeşile döndü.

## Handoff

- None
