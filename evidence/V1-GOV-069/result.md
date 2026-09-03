# V1-GOV-069 - Wave 23 master audit reseal and gate closure

- Date: 2026-09-01

## Scope

Reseal `GATE-V1-EXIT` after wave 23 (`V1-RMD-096`: Host-terminated HTTPS with a
self-signed fallback certificate, closing pre-go-live finding E1).

## Test evidence (2026-09-01, compose stack stopped, tests use `alkaros-pg`)

| Suite | Command | Result |
| --- | --- | --- |
| C# full | `dotnet test ALKAROS.slnx` (Docker `alkaros-sdk10-rt8` + `alkaros-pg`, `--memory=4g`) | 46 test projects, 1100 passed, 0 failed (`FULLTEST_EXIT=0`) |
| DualScreen (targeted) | `dotnet test --filter FullyQualifiedName~DualScreen` | 27 passed (6 new TLS tests incl. SAN dedupe) |
| PosTerminal vitest | `corepack pnpm run test` | 15 files, 96 passed |
| PosTerminal types / build | `corepack pnpm run typecheck` / `build` | exit 0 |
| Architecture | `python3 -m pytest tests/Architecture -q` (Docker) | 208 passed |
| Turkish-in-code | `python tools/consistency-audit/consistency_audit.py` | clean |
| Compose | `docker compose config -q` | valid |
| Plan integrity | `python tools/plan-audit/plan_audit_tool.py validate` | 0 errors, 0 warnings (554 md, 532 task files) |
| Manifest | `generate-audit-report` -> `generate-manifest` -> `verify-manifest` | 211 baseline rows, 639 added files, `Manifest errors: 0` |

## Functional evidence

`evidence/V1-RMD-096/`:

- `https-live-check.log`: `https://127.0.0.1:5443/` returns 200 with no
  `X-Forwarded-Proto`; `http://127.0.0.1:5080/` returns 400 `HTTPS_REQUIRED`;
  self-signed cert SAN `DNS:pos.lan, DNS:localhost, IP Address:127.0.0.1,
  IP Address:::1`.
- `dotnet test` DualScreen filter 26/0 including
  `HostTerminatesHttpsWithASelfSignedCertificateAndNeedsNoForwardedProto`.

## Governance

- `plan/GATES.md` line 36 updated to the sealed wave-23 form; wave-23 reopen +
  reseal narratives appended.
- `plan/v1/README.md` matrix: **244 tasks - 239 Done, 5 approved NotApplicable,
  0 Planned, 0 InProgress.**
- `V1-RMD-096` -> Done; `V1-GOV-068` / `V1-GOV-069` -> Done.

`GATE-V1-EXIT` is sealed.
