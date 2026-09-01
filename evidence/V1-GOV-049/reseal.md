# V1-GOV-049 - Wave 13 master audit reseal

- Date: 2026-09-01
- Owner: claude-code-019uHsMymyuC1tZ5DHtmWsRi
- Covers: 13. dalga (`V1-RMD-086` PostgreSQL yedekleme/geri yükleme mekanizması).

`V1-RMD-086` yalnızca `deploy/docker/*.sh`, `docs/recovery/**`, `compose.yaml` ve
plan dosyalarını değiştirdi; `src/**` ve `tests/**` altında kod değişikliği yok.
Tam test suite yine de provenance için çalıştırıldı.

## Test suites

| Suite | Command | Result |
| --- | --- | --- |
| C# | `dotnet test ALKAROS.slnx` (Docker `alkaros-sdk10-rt8` + `alkaros-pg`) | 46 proje, 1086 test, **0 failed** |
| PosTerminal test | `pnpm --dir src/Clients/PosTerminal run test` | 14 dosya, 90 test, exit 0 |
| PosTerminal typecheck | `pnpm --dir src/Clients/PosTerminal run typecheck` | exit 0 |
| PosTerminal build | `pnpm --dir src/Clients/PosTerminal run build` | exit 0 |
| Architecture | `python3 -m pytest tests/Architecture` (Docker) | 208 test, **0 failed** |
| Backup round-trip | `deploy/docker/backup-restore-selfcheck.sh` (postgres:18) | PASS, exit 0 (500/500 satır, veri md5 eşleşir, bozuk artefakt exit 4) |
| Consistency | `python tools/consistency-audit/consistency_audit.py` | `consistency-audit: clean` |
| Plan integrity | `python tools/plan-audit/plan_audit_tool.py validate` | 0 errors, 0 warnings |
| Manifest | `python tools/plan-audit/plan_audit_tool.py verify-manifest` | OK |

Ham loglar: `dotnet-full-suite.log`, `pytest-architecture.log`, `backup-restore-selfcheck.log`.

## Gate

`GATE-V1-EXIT` `plan/GATES.md` (satır 36 + 2026-09-01 13. dalga reseal notu) ve
`plan/v1/README.md` üzerinde kesin olarak mühürlendi.

V1 matrisi: 222 görev — 217 `Done`, 5 onaylı `NotApplicable`, 0 `Planned`, 0 `InProgress`.
