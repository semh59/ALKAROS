# V1-GOV-063 - Wave 20 master audit reseal

- Date: 2026-09-01
- Owner: claude-code-019uHsMymyuC1tZ5DHtmWsRi
- Covers: 20. dalga (`V1-RMD-093` yazma kritik yolu yük testi).

## Değişen yüzey

- `tools/load-test/load_test.py` — `--scenario write` gerçek yazma senaryosu (seat/item/submit + revision takibi).
- `tools/load-test/seed-loadtest.sh` — yeni, üretim boyutlu seed + `--clean`.
- `docs/performance/critical-path-load-v1.md` — yeni.
- `docs/compliance/accessibility-target.md` — Semih onayı işlendi (V0-CMP-005-D001, EXC-001).
- `docs/qa/device-browser-test-plan.md` — onay notu güncellendi.

RMD-093 `src/**`/`tests/**` kodu değiştirmedi. Tam suite yine de provenance için çalıştırıldı.

## Test suites

| Suite | Command | Result |
| --- | --- | --- |
| C# tam | `dotnet test ALKAROS.slnx` (Docker `alkaros-sdk10-rt8` + `alkaros-pg`) | 46 proje, **0 failed** |
| PosTerminal test / typecheck / build | `pnpm ...` | 96 test, exit 0 / 0 / 0 |
| Architecture | `python3 -m pytest tests/Architecture` (Docker) | 208 test, **0 failed** |
| Consistency | `tools/consistency-audit/consistency_audit.py` | clean |
| Plan integrity | `plan_audit_tool.py validate` | 0 errors, 0 warnings |
| Manifest | `plan_audit_tool.py verify-manifest` | 0 errors |

## Yazma kritik yolu yük testi sonucu

1M sipariş + 3M kalem arka plan verisi üzerinde 20 terminal, 40 sn, temiz koşu:
sipariş gönderimi p95 45.9 ms, p99 73.9 ms, hata %0 — `V15-PER-001` hedefini
~10x/~13x marjla karşılar. `pg_stat_database.deadlocks` 0→0, 60 kilit-anket
örneğinin tamamında kilit bekleyen yok. Seed verisi test sonrası temizlendi,
canlı veritabanı tam olarak öncesi duruma döndü (`tables=3 products=1 orders=2`).

## Gate

`GATE-V1-EXIT` `plan/GATES.md` (satır 36 + 2026-09-01 20. dalga reseal notu) ve
`plan/v1/README.md` üzerinde kesin olarak mühürlendi.

V1 matrisi: 236 görev — 231 `Done`, 5 onaylı `NotApplicable`, 0 `Planned`, 0 `InProgress`.
