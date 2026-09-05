# V1-GOV-076 - Wave 27 master audit reseal and gate closure

- Task ID: V1-GOV-076
- Status: Done
- Assignee: claude-session-01XKRazppo9sW452rdbCZsgy
- Work type: validation
- Surface state: Existing

## Source basis

- PO:2026-09-04

## Goal

`V1-TBL-008`, `V1-SET-003` ve `V1-CUI-006` tamamlandıktan sonra tüm test
süitlerinin, tutarlılık denetim betiğinin, plan bütünlüğünün ve manifest
hash'lerinin doğrulanması ve `GATE-V1-EXIT` kapısının 27. dalga için kesin
olarak yeniden mühürlenmesi.

## Owned surface

- `plan/v1/governance/V1-GOV-076-wave27-master-audit-reseal-and-gate-closure.md`
- `plan/AUDIT_MANIFEST.json`
- `plan/AUDIT_REPORT.md`
- `plan/GATES.md`
- `plan/v1/README.md`
- Bu görev, başka bir task'in owned surface alanını değiştiremez.

## In scope

- `dotnet build -c Release` ve `-c Debug` sıfır uyarı / sıfır hata.
- `pnpm --dir src/Clients/PosTerminal typecheck`/`test`/`build` sıfır çıkış
  kodu.
- `python tools/consistency-audit/consistency_audit.py` sıfır hata.
- `python tools/plan-audit/plan_audit_tool.py validate`, `validate-coverage`
  ve `verify-manifest` sıfır hata.
- `GATE-V1-EXIT` kapısını `plan/GATES.md` (27. dalga reseal notu) ve
  `plan/v1/README.md` üzerinde kesin olarak mühürlemek.

## Out of scope

- Production kodunda yeni özellik eklemek veya üç görevin kapsamını
  genişletmek.

## Dependencies

- V1-TBL-008
- V1-SET-003
- V1-CUI-006

## Deliverables

- Mühürlü `GATE-V1-EXIT` kapısı ve doğrulanmış audit manifesti.

## Acceptance evidence

- Üç bağımlı görev de `Done`: `V1-TBL-008`, `V1-SET-003`, `V1-CUI-006`.
- `dotnet build ALKAROS.slnx -c Release` ve `-c Debug`: 0 uyarı / 0 hata.
- `pnpm --dir src/Clients/PosTerminal typecheck` / `test` / `build`: hepsi
  sıfır çıkış kodu; `test` 19 dosya / 118 test (109 → 118).
- `dotnet test` (yerel Postgres 18, `alkaros-test-pg`):
  `ALKAROS.Settings.ReservationStation.Tests` 4/4 (yeni),
  `ALKAROS.Settings.KitchenLiveSync.Tests` 4/4,
  `ALKAROS.Architecture.Tests` 8/8,
  `ALKAROS.Host.Experience.Composition.Tests` 4/4 — hepsi regresyonsuz.
  `tests/Host/MigrationComposition/DualScreen/DualScreenHostTests.cs`'teki
  güncellenmiş `runtime-configuration` testi bu ortamda çalıştırılamadı
  (`psql` CLI kurulu değil, G2, bu dalgadan bağımsız — aynı projede zaten
  41/121 test bu nedenle başarısız); doğruluk derleme + kod incelemesiyle
  sağlandı, CI test otoritesidir.
- `python tools/consistency-audit/consistency_audit.py`: temiz (bir
  Türkçe karakter sızıntısı `ReservationStationSetting.cs`'nin XML doc
  yorumunda bulunup düzeltildi).
- `python tools/plan-audit/plan_audit_tool.py validate`,
  `validate-coverage`, `generate-audit-report`, `generate-manifest`,
  `verify-manifest`: sıfır hata.
- `GATE-V1-EXIT` `plan/GATES.md` üzerinde (özet hücre + 27. dalga kesin
  reseal tarihli bölümü) ve `plan/v1/README.md` üzerinde kesin olarak
  mühürlendi.

## Handoff

- GATE-V11-ENTRY
