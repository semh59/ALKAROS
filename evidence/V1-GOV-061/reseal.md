# V1-GOV-061 - Wave 19 master audit reseal

- Date: 2026-09-01
- Owner: claude-code-019uHsMymyuC1tZ5DHtmWsRi
- Covers: 19. dalga (`V1-RMD-092` cihaz/tarayıcı test planı ve vanilla istemci a11y smoke).

## Değişen yüzey

- `docs/qa/device-browser-test-plan.md` — yeni.
- `src/Clients/PosTerminal/src/vanilla-clients-a11y.test.ts` — yeni (6 test).
- `src/Clients/WaiterPwa/wwwroot/index.html` — viewport meta `user-scalable=no` / `maximum-scale=1.0` kaldırıldı (WCAG 1.4.4 AA).

## Test suites

| Suite | Command | Result |
| --- | --- | --- |
| Vanilla a11y smoke (hedefli) | `pnpm exec vitest run src/vanilla-clients-a11y.test.ts` | 6 passed, 0 failed |
| C# tam | `dotnet test ALKAROS.slnx` (Docker `alkaros-sdk10-rt8` + `alkaros-pg`) | 46 proje, **0 failed** |
| PosTerminal test / typecheck / build | `pnpm ...` | 15 dosya / **96 test**, exit 0 / 0 / 0 |
| Architecture | `python3 -m pytest tests/Architecture` (Docker) | 208 test, **0 failed** |
| Consistency | `tools/consistency-audit/consistency_audit.py` | clean |
| Plan integrity | `plan_audit_tool.py validate` | 0 errors, 0 warnings |
| Manifest | `plan_audit_tool.py verify-manifest` | 0 errors |

Ham loglar: `dotnet-full-suite.log`, `pytest-architecture.log`; smoke kanıtı
`evidence/V1-RMD-092/`.

## Bulunan ve düzeltilen defekt

Vanilla a11y smoke ilk çalıştırmada Waiter PWA'da `meta-viewport` (critical) axe
ihlali yakaladı: `user-scalable=no, maximum-scale=1.0` pinch-zoom'u engelliyordu
(WCAG 2.2 1.4.4 Resize Text AA). Viewport meta düzeltildi; smoke temiz geçti.
Cashier POS: sıfır critical/serious, değişiklik yok.

## Açık kalan

`docs/compliance/accessibility-target.md` WCAG seviye kararı ve EXC-001 onayı
Semih'i bekliyor (decision görevi). Fiziksel cihaz testi manuel kontrol listesi
olarak kalır (`docs/qa/device-browser-test-plan.md` §3).

## Gate

`GATE-V1-EXIT` `plan/GATES.md` (satır 36 + 2026-09-01 19. dalga reseal notu) ve
`plan/v1/README.md` üzerinde kesin olarak mühürlendi.

V1 matrisi: 234 görev — 229 `Done`, 5 onaylı `NotApplicable`, 0 `Planned`, 0 `InProgress`.
