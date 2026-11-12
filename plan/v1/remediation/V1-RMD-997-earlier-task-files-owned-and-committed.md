# V1-RMD-997 - Önceki görevlerin yeni dosyaları sahiplenir ve istemci derleme kuralı commit edilir

- Task ID: V1-RMD-997
- Status: Done
- Assignee: claude-code-session_019vXeYYn5EiQaDrvjG9cpob
- Work type: implementation
- Surface state: Existing

## Goal

985, 990, 992 ve 994 görevlerinin eklediği yeni dosyalar bir görevin sahipliğine alınır; 991 görevinin kapanışta commit dışı kalan kural ve betik dosyaları (AGENTS.md, TASK_STANDARD.md, task-scope iş akışı, istemci derleme kapısı) kayıt altına girer.

## Owned surface

- `evidence/V1-RMD-997/**`
- `src/Clients/PosTerminal/src/focusOnMount.ts`
- `src/Clients/PosTerminal/src/tableCart.ts`
- `src/Clients/PosTerminal/src/routes/Cashier.stale-order.test.tsx`
- `src/Clients/PosTerminal/src/routes/Cashier.table-cart.test.tsx`
- `src/Clients/PosTerminal/src/routes/loginFocus.test.tsx`
- `src/Host/Experience/Reporting/KitchenlessAddedCounter.cs`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - AGENTS.md
  - plan/TASK_STANDARD.md
  - .github/workflows/task-scope.yml
  - tools/client-build-gate/clientBuildGate.js
  - tools/client-build-gate/clientBuildGate.test.js
  - tools/client-build-gate/fixtures/declaredFirst.tsx
  - tools/client-build-gate/fixtures/useBeforeDeclare.tsx
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Dosyaların sahiplik kaydı ve commit edilmesi (içerik 985/990/991/992/994 görevlerinde test edilip incelenmiştir)
2. Kapı betiğinin node testinin yeniden koşulması

## Out of scope

- Dosya içeriğinde davranış değişikliği

## Dependencies

- V1-RMD-995

## Acceptance evidence

1. Kapı betiği node testi çıktısı ve plan-audit sonucu

## Handoff

- None
