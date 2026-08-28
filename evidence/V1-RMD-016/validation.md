# V1-RMD-016 validation evidence

- Task owner: `/root/rmd016_prod_shell`
- Repository root: `D:/PROJECT/ALKAROS`
- Validation date: `2026-08-26`

## Preflight and write boundary

The initial `git status --short` and `git diff --name-only` snapshot contained pre-existing changes outside this task,
including `src/Clients/PosTerminal/package.json`, `pnpm-lock.yaml`, `src/App.tsx`, `src/styles.css`, host files, plan
files, and other task evidence. Those paths were treated as user-owned baseline and were not edited by V1-RMD-016.

Exact task write allowlist:

- `src/Clients/PosTerminal/src/shell/**`
- `src/Clients/PosTerminal/src/design-system/**`
- `plan/v1/remediation/V1-RMD-016-production-shell-and-design-system.md` metadata only
- `evidence/V1-RMD-016/**`

## Commands and exit codes

| Command | Exit code | Result |
| --- | ---: | --- |
| `pnpm exec vitest run src/shell/ProductionShell.test.tsx src/shell/layout-contract.test.ts src/design-system/primitives.test.tsx` | 0 | 3 files, 17 tests passed |
| `pnpm test` | 0 | 4 files, 26 tests passed |
| `pnpm typecheck` | 0 | TypeScript emitted no errors |
| `pnpm build` | 0 | Vite production build completed; 45 modules transformed |
| `python -B tools/plan-audit/plan_audit_tool.py validate` | 0 | 394 task files, 1350 dependency edges, 0 errors, 0 warnings |
| `git diff --check` | 0 | No whitespace errors in tracked diff |
| prohibited pattern scan over owned source | 0 | No `TODO`, `FIXME`, `placeholder`, `mock`, or broad catch block |

The bundled Codex Node runtime was prepended to `PATH` because the repository `pnpm` launcher was available while the
host `node` executable was not initially present in `PATH`. No dependency, package manifest, lockfile, or Vite
configuration was changed.

## Source SHA-256

| File | SHA-256 |
| --- | --- |
| `src/Clients/PosTerminal/src/design-system/index.ts` | `66b0dcf4762ad68169eacb026a3e1d5823660c3175f1e5f117749cb93ab18cf1` |
| `src/Clients/PosTerminal/src/design-system/primitives.css` | `14544c5fa0af1538e6efb07d43db04067f0b7b792d7e4a9e6aaf11f89cded98a` |
| `src/Clients/PosTerminal/src/design-system/primitives.test.tsx` | `c20738a0ac059c985b1a37ac0cc30fd65e6389db1b6f53a03ce7356c59c42293` |
| `src/Clients/PosTerminal/src/design-system/primitives.tsx` | `ed2c2125b41e5c191f3da52b60fec2b31c5814ee0207816d84f8e011093b8b72` |
| `src/Clients/PosTerminal/src/design-system/tokens.css` | `b7182d47b473ada7ce8a25d7e67c7685aa250bfa84ca34c6a25c7cb2c1e0ea40` |
| `src/Clients/PosTerminal/src/shell/index.ts` | `7751d724cfb880c0f19f8509ccea7862037a38d046178351e74ae597ff0acdd0` |
| `src/Clients/PosTerminal/src/shell/layout-contract.test.ts` | `f66eb5ad0dfaae9fae28c4c6ee9950201e3a6f33a8e7346e9ae6909e804d1e2f` |
| `src/Clients/PosTerminal/src/shell/models.ts` | `905ae1927c3a49b5b925b74619ee637a39bc0126915d8a29576e9fa003402fee` |
| `src/Clients/PosTerminal/src/shell/ProductionShell.test.tsx` | `a356c0e4408810dedeecc59a8f18eee7d04b11ed6d48abd196edbf77fbf770c0` |
| `src/Clients/PosTerminal/src/shell/ProductionShell.tsx` | `60a92076391615ce86c3febafa126bc9bec63b8bdff729305508948a7bbfe399` |
| `src/Clients/PosTerminal/src/shell/shell.css` | `9623f4da4ba3e5150814da7a9b61a8bfb2c07a64114e8c01c1c8e7f892a351b3` |

## Manual acceptance scenario

At `1280x800`, `768x1024`, and `390x844`, compose the shell with authoritative session identity, capabilities,
connectivity, and freshness values. Use keyboard and touch to navigate allowed routes. Confirm the 1280 layout has a
persistent rail/context drawer, 768 uses a compact rail/sheet, and 390 uses bottom navigation/full-screen drill-down.
Change the supplied capability set and confirm only allowed navigation remains. Exercise missing/expired session and
forbidden route independently, then set connectivity to offline and freshness to stale; the distinct recovery status
must stay visible at every viewport. Verify focus enters and returns from dialogs/sheets, Escape closes them, and no
horizontal page overflow appears.
