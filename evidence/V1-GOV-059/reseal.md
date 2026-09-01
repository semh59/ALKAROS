# V1-GOV-059 - Wave 18 master audit reseal

- Date: 2026-09-01
- Owner: claude-code-019uHsMymyuC1tZ5DHtmWsRi
- Covers: 18. dalga (`V1-RMD-091` operasyonel veri housekeeping sweep).

## Değişen yüzey

- `src/Host/Program.cs` — `housekeeping --db-url <url> [--grace-days N]` fiili.
- `tests/Host/MigrationComposition/Program/HousekeepingTests.cs` — 4 test (yeni).
- `compose.yaml` — `ops` profili `housekeeping` servisi.
- `docs/operations/data-housekeeping.md` — yeni.

## Test suites

| Suite | Command | Result |
| --- | --- | --- |
| Housekeeping (hedefli) | `dotnet test tests/Host/MigrationComposition --filter Housekeeping` | 4 passed, 0 failed |
| C# tam | `dotnet test ALKAROS.slnx` (Docker `alkaros-sdk10-rt8` + `alkaros-pg`) | 46 proje, **0 failed** |
| PosTerminal test / typecheck / build | `pnpm ...` | 90 test, exit 0 / 0 / 0 |
| Architecture | `python3 -m pytest tests/Architecture` (Docker) | 208 test, **0 failed** |
| Consistency | `tools/consistency-audit/consistency_audit.py` | clean |
| Compose | `docker compose config` | valid |
| Plan integrity | `plan_audit_tool.py validate` | 0 errors, 0 warnings |
| Manifest | `plan_audit_tool.py verify-manifest` | 0 errors |

Ham loglar: `dotnet-full-suite.log`, `pytest-architecture.log`; fiil kanıtı
`evidence/V1-RMD-091/`.

## Kapsam

Operasyonel veritabanı hijyeni: süresi dolmuş `idempotency_keys` ve süresi dolmuş
/ uzun süredir iptal edilmiş `identity.device_sessions` (`session_operations`
çocuklarıyla) silinir. KVKK kişisel veri saklama/anonimleştirme kapsam dışıdır
(`V15-KVK-001` / `V15-KVK-002`, `V0-CMP-003` envanteri); `orders`/`bills`/fiscal/
invoice hiç dokunulmaz.

## Gate

`GATE-V1-EXIT` `plan/GATES.md` (satır 36 + 2026-09-01 18. dalga reseal notu) ve
`plan/v1/README.md` üzerinde kesin olarak mühürlendi.

V1 matrisi: 232 görev — 227 `Done`, 5 onaylı `NotApplicable`, 0 `Planned`, 0 `InProgress`.
