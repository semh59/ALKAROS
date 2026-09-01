# V1-GOV-053 - Wave 15 master audit reseal

- Date: 2026-09-01
- Owner: claude-code-019uHsMymyuC1tZ5DHtmWsRi
- Covers: 15. dalga (`V1-RMD-088` dağıtım altyapısı performans ayarı).

`V1-RMD-088` yalnızca `deploy/docker/postgresql.tuned.conf`, `compose.yaml`,
`docs/performance/infra-tuning.md` ve plan dosyalarını değiştirdi; `src/**` ve
`tests/**` altında kod değişikliği yok. Tam test suite yine de provenance için
çalıştırıldı.

## Test suites

| Suite | Command | Result |
| --- | --- | --- |
| C# | `dotnet test ALKAROS.slnx` (Docker `alkaros-sdk10-rt8` + `alkaros-pg`) | 46 proje, 1086 test, **0 failed** |
| PosTerminal test | `pnpm --dir src/Clients/PosTerminal run test` | 14 dosya, 90 test, exit 0 |
| PosTerminal typecheck / build | `pnpm run typecheck` / `build` | exit 0 / exit 0 |
| Architecture | `python3 -m pytest tests/Architecture` (Docker) | 208 test, **0 failed** |
| Load (ayar sonrası) | `tools/load-test/load_test.py` 20 terminal @ 3 rps | p95 4.1 ms, p99 6.6 ms, err %0 → `V15-PER-001` hedefini karşılar, regresyon yok (öncesi p95 4.3 / p99 9.3) |
| Consistency | `python tools/consistency-audit/consistency_audit.py` | `consistency-audit: clean` |
| Compose | `docker compose config` | valid |
| Plan integrity | `plan_audit_tool.py validate` | 0 errors, 0 warnings |
| Manifest | `plan_audit_tool.py verify-manifest` | 0 errors |

Ham loglar: `dotnet-full-suite.log`, `pytest-architecture.log`; ayar öncesi/sonrası
`evidence/V1-RMD-088/before-tuning.*`, `after-tuning.*`, `comparison.md`.

## Uygulanan ayar (canlı doğrulandı)

- PostgreSQL: `shared_buffers=384MB`, `work_mem=16MB`, `random_page_cost=1.1`,
  `jit=off`, `wal_compression=zstd`, `effective_io_concurrency=256`,
  `max_connections=200`, `idle_in_transaction_session_timeout=60s`,
  `autovacuum_vacuum_scale_factor=0.05`.
- `host`: `DOTNET_gcServer=1`, `DOTNET_GCDynamicAdaptationMode=1`,
  `DOTNET_TieredPGO=1` (konteyner içinde doğrulandı).
- `deploy.resources` sınırları: postgres 4 CPU / 2 GB, host 4 CPU / 1.5 GB;
  postgres `shm_size: 512mb`.

## Gate

`GATE-V1-EXIT` `plan/GATES.md` (satır 36 + 2026-09-01 15. dalga reseal notu) ve
`plan/v1/README.md` üzerinde kesin olarak mühürlendi.

V1 matrisi: 226 görev — 221 `Done`, 5 onaylı `NotApplicable`, 0 `Planned`, 0 `InProgress`.
