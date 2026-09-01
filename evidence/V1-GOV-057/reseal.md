# V1-GOV-057 - Wave 17 master audit reseal

- Date: 2026-09-01
- Owner: claude-code-019uHsMymyuC1tZ5DHtmWsRi
- Covers: 17. dalga (`V1-RMD-090` oturtmada güncelliğini yitirmiş masa sürümü toleransı).

## Değişen yüzey

- `src/Host/DualScreen/DualScreenStore.cs` — `StartOrderAsync` içindeki `ExpectedTableRowVersion` eşitlik kapısı kaldırıldı; oturtma kararı `FOR UPDATE` kilidi altında taze durumdan verilir.
- `tests/Host/MigrationComposition/DualScreen/DualScreenStoreTests.cs` — test yeniden yazıldı + yeni test.
- `plan/v1/remediation/V1-RMD-078-*.md` — NotApplicable gerekçesine sektör pratiği notu.

## Test suites

| Suite | Command | Result |
| --- | --- | --- |
| DualScreen (hedefli) | `dotnet test tests/Host/MigrationComposition --filter DualScreen` | 21 passed, 0 failed |
| C# tam | `dotnet test ALKAROS.slnx` (Docker `alkaros-sdk10-rt8` + `alkaros-pg`) | 46 proje, **0 failed** |
| PosTerminal test / typecheck / build | `pnpm ...` | 90 test, exit 0 / 0 / 0 |
| Architecture | `python3 -m pytest tests/Architecture` (Docker) | 208 test, **0 failed** |
| Consistency | `tools/consistency-audit/consistency_audit.py` | clean |
| Plan integrity | `plan_audit_tool.py validate` | 0 errors, 0 warnings |
| Manifest | `plan_audit_tool.py verify-manifest` | 0 errors |

Not: makine belleği dar (8 GB, diğer yığınlarla paylaşımlı); test koşuları sırasında
Compose yığını (`host`/`proxy`/`postgres`, testler ayrı `alkaros-pg` kullanır)
durdurularak bellek açıldı.

Ham loglar: `dotnet-full-suite.log`, `pytest-architecture.log`.

## Davranış değişikliği

Güncelliğini yitirmiş `ExpectedTableRowVersion` + `Available` masa → oturtma
başarılı (önceden: "Table row version is stale" 409). Gerçek engeller (masa pasif,
adisyonlu, başka terminalde aktif, `Available` değil) → yine spesifik
`DualScreenConflictException`.

## Gate

`GATE-V1-EXIT` `plan/GATES.md` (satır 36 + 2026-09-01 17. dalga reseal notu) ve
`plan/v1/README.md` üzerinde kesin olarak mühürlendi.

V1 matrisi: 230 görev — 225 `Done`, 5 onaylı `NotApplicable`, 0 `Planned`, 0 `InProgress`.
