# V1-RMD-991 - İstemci üretim derlemesi kapanış şartı olur

- Task ID: V1-RMD-991
- Status: InProgress
- Assignee: claude-code-session_019vXeYYn5EiQaDrvjG9cpob
- Work type: implementation
- Surface state: Existing

## Goal

src/Clients altındaki bir istemci değişen her görevde lint (sıfır uyarı), tip denetimi ve üretim derlemesi kapanış şartıdır; kural AGENTS.md ve TASK_STANDARD.md'ye yazılır, tools altındaki bir denetim betiği ve görev-kapsam CI'ı ile uygulanır ve yalnız lint'in yakaladığı kırık örnek test edilir.

## Owned surface

- `evidence/V1-RMD-991/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - AGENTS.md
  - plan/TASK_STANDARD.md
  - .github/workflows/task-scope.yml
  - tools/client-build-gate/clientBuildGate.js
  - tools/client-build-gate/clientBuildGate.test.js
  - tools/client-build-gate/fixtures/useBeforeDeclare.tsx
  - tools/client-build-gate/fixtures/declaredFirst.tsx
  - src/Clients/PosTerminal/package.json
  - src/Clients/PosTerminal/.oxlintrc.json
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi

## Out of scope

- Listede olmayan ekranlar ve dosyalar

## Dependencies

- V1-RMD-990

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
