# V1-RMD-024 scope failure

- Date: 2026-08-27
- Task: `V1-RMD-024`
- Verdict: **BLOCKED — write-set failure**

## Valid source checks before failure

- Focused `layout-contract.test.ts`: 5/5 passed.
- Full PosTerminal suite: 10 files, 65 tests passed.
- `pnpm run typecheck`: exit `0`.
- Source writes before build were limited to `shell.css` and `layout-contract.test.ts`.

## Failure

The attempted evidence-scoped build passed an extra literal `--` to Vite. Vite ignored the requested output directory
and rewrote `src/Clients/PosTerminal/dist/index.html` plus its hashed JS/CSS assets. That directory was not in the task
owned surface, so the task cannot close even though the build itself returned exit `0`.

## Recovery

The owned CSS addition was removed and Vite rebuilt the pre-task source. The resulting artifact names were
the same names observed before V1-RMD-024:

- `dist/assets/index-CgU1nhAH.js`
- `dist/assets/index-Ce2tG2yq.css`

The owned CSS and layout-test behavior additions were then removed from the working tree. Patch tooling changed the
files' byte layout, so their SHA-256 values did not return to the preflight values; no exact-hash restoration is claimed.
Current hashes are `E9A800951BDD8F1A03BFAFE1DCCE3A6D57497FFCE8E5158D0E40ACB192A1DB1F` for `shell.css` and
`F4575BC5891F66FCE8728016E6BCC5F81115EF808DC15275BBF76A14A21A019A` for `layout-contract.test.ts`. No production
bundle containing the V1-RMD-024 behavior change is claimed as valid evidence. A new exact-custody task is required to
reapply the source change, generate its bundle inside that task's evidence directory, and rerun real browser validation.
