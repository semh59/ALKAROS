# V1-GOV-070 - Wave 24 master audit reseal validation

Reseal of `GATE-V1-EXIT` after `V1-RMD-098` (Docker frontend/backend split, A1-full)
and its follow-up fixes. Date: 2026-09-03.

## CI - production-validation (authoritative for the test suites)

Every wave-24 commit ran green, 22/22 steps:

| run | commit | subject | result |
| --- | --- | --- | --- |
| #50 | `b60f58d` | GetByX defensive row ceilings | success |
| #51 | `12bbc4e` | Docker rebuilt as a frontend/backend split (A1-full) | success |
| #56 | `a13c7a5` | WaiterPwa staff sign-in + /waiter routing | success |
| #57 | `00e7dbc` | reach the stack from a phone (LAN IP host + no-SNI TLS) | success |
| #58 | `3150fa5` | demo seed + WaiterPwa login fits a phone; dev no-cache | success |
| #59 | `417808b` | WaiterPwa crypto.randomUUID plain-HTTP crash fix | success |

Steps covered: exact toolchain, plan validate + PDF trace + project manifest,
root markdown lint, locked frontend install + vitest + tsc + vite build,
node contract tests, python architecture tests, locked .NET restore + Release
build, build provenance (positive + negative), digest-pinned PostgreSQL 18,
full .NET tests with line/branch coverage (45 assemblies, ~1106 tests, 0 fail),
dependency vulnerability scan, SBOM + license inventory, full git-history secret
scan.

## Local verification

- `dotnet build ALKAROS.slnx -c Release --no-restore` -> 0 warning / 0 error.
- `dotnet build ALKAROS.slnx -c Debug --no-restore` -> 0 warning / 0 error.
- `python tools/consistency-audit/consistency_audit.py` -> `consistency-audit: clean`.
- `python tools/plan-audit/plan_audit_tool.py validate` -> Validation errors: 0, warnings: 0.
- `python tools/plan-audit/plan_audit_tool.py validate-coverage` -> Coverage errors: 0.
- `python tools/plan-audit/plan_audit_tool.py verify-manifest` -> Manifest errors: 0.
- `docker compose config` -> valid.
- `docker compose -f compose.yaml -f compose.ops.yaml config` -> valid.
- `docker compose -f compose.yaml -f compose.dev.yaml config` -> valid.
- End-to-end live container test: `evidence/V1-RMD-098/compose-transcript.txt`
  (up --build --wait exit 0; 41 migrations; all client routes + SPA deep links
  200 over HTTPS; B-4 isolation verified three ways; HTTPS_REQUIRED gate holds;
  ops backup artifact + sha256; api restart clean; down -v clean).

SAC (Windows Smart App Control) blocks freshly built managed test DLLs locally,
so the CI Linux run is the authority for `dotnet test` / `pnpm test`.

## Gate

`GATE-V1-EXIT` reopened by `V1-RMD-098` and resealed here. V1 matrix: 246 tasks,
241 `Done`, 5 approved `NotApplicable`, 0 `Planned`, 0 `InProgress`.

Deferred to a future task (not blocking this seal): a real domain +
Cloudflare DNS-01 real Let's Encrypt certificate, for cert-free HTTPS + offline
on waiter phones. Until then the dev overlay serves plain HTTP and the prod path
uses `tls internal`.
