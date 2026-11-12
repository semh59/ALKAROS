# V1-RMD-991 Result

The production build of a client (lint with zero warnings, type check, vite build) is now a closing rule for any task that changes src/Clients/**.
- Rule written in AGENTS.md (closing gate) and plan/TASK_STANDARD.md (acceptance evidence).
- tools/client-build-gate/clientBuildGate.js finds the changed clients (git diff against a base commit plus untracked files) and runs the three steps; it stops at the first failure. The CI job already ran pnpm build for PosTerminal; the script's test now runs in the Node contract tests step of .github/workflows/task-scope.yml.
- PosTerminal: lint cap 20 -> 0 and the transition override list of .oxlintrc.json removed (it only downgraded violations that 990 fixed; lint still passes with no diagnostics).
- Broken example: fixtures/useBeforeDeclare.tsx is the V1-RMD-983 mistake (state setter read before its declaration). The production lint script rejects it (exit 1, react immutability) and accepts declaredFirst.tsx. The incident shows vitest and tsc accepted the same mistake.

- green.log: 6 of 6 gate tests pass. green-gate.log: gate on this change exit 0 (lint, typecheck, vite build). red-gate.log: a broken file dropped into PosTerminal makes the gate exit 1.
- mutations.txt: 5 mutations killed (M1 and M5 re-run after the review made the Windows path test real), files restored.
