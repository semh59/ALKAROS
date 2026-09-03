# V1-GOV-070 - Wave 24 master audit reseal and gate closure

- Task ID: V1-GOV-070
- Status: Done
- Assignee: claude-code-01XKRazppo9sW452rdbCZsgy
- Work type: validation
- Surface state: Planned

## Source basis

- PO:2026-09-03

## Goal

`V1-RMD-098` 24. dalga Docker arayüz/backend ayrımı (A1-full) görevi tamamlandıktan sonra tüm test
süitlerinin (C#, Vitest, pytest), tutarlılık denetim betiğinin, plan bütünlüğünün, `docker compose config`
geçerliliğinin (üç overlay kombinasyonu) ve manifest hash'lerinin doğrulanması ve `GATE-V1-EXIT` kapısının
kesin olarak yeniden mühürlenmesi.

## Owned surface

- `plan/v1/governance/V1-GOV-070-wave24-master-audit-reseal-and-gate-closure.md`
- `plan/AUDIT_MANIFEST.json`
- `plan/AUDIT_REPORT.md`
- `plan/GATES.md`
- `plan/v1/README.md`
- `evidence/V1-GOV-070/**`

## In scope

- CI `production-validation` pipeline'ının 24. dalga commit'lerinde (Docker A1-full split → randomUUID
  düzeltmesi) 22 adımın tamamıyla yeşil olduğunu doğrulamak: `dotnet restore --locked-mode` + Release
  build, `Full .NET tests with line and branch coverage`, `Locked frontend install, tests and production
  build` (PosTerminal tsc + vitest + vite build), `Node contract tests`, `Python architecture tests`,
  `Verify build provenance`, `Dependency vulnerability scan`, `SBOM and license inventory`, `Full Git
  history secret scan`.
- `python tools/consistency-audit/consistency_audit.py` sıfır hata.
- `python tools/plan-audit/plan_audit_tool.py validate`, `validate-coverage` ve `verify-manifest` sıfır
  hata; `plan/AUDIT_REPORT.md` ve `plan/AUDIT_MANIFEST.json` mevcut ağaç durumuna göre yeniden üretilir.
- `docker compose config`, `docker compose -f compose.yaml -f compose.ops.yaml config` ve
  `docker compose -f compose.yaml -f compose.dev.yaml config` geçerli.
- Yerel Release + Debug `dotnet build ALKAROS.slnx` sıfır uyarı / sıfır hata.
- Uçtan uca canlı Docker testi `evidence/V1-RMD-098/compose-transcript.txt` ile kanıtlanmıştır (SAC yerel
  .NET test kısıtı nedeniyle test otoritesi CI'dır).
- `GATE-V1-EXIT` kapısını `plan/GATES.md` (kayıt satırı + 24. dalga reseal bölümü) ve `plan/v1/README.md`
  (görev matrisi 244 → 246, 241 `Done`) üzerinde kesin olarak mühürlemek.

## Out of scope

- Production kodunda yeni özellik eklemek veya `V1-RMD-098` kapsamını genişletmek.
- Gerçek bir alan adı + Cloudflare DNS-01 gerçek sertifika kurulumu (Semih alan adını
  aldığında ayrı bir görevle yapılacak; şu an düz HTTP dev overlay + `tls internal` prod yolu geçerli).

## Dependencies

- V1-RMD-098

## Deliverables

- Mühürlü `GATE-V1-EXIT` kapısı ve doğrulanmış audit manifesti.

## Acceptance evidence

- CI `production-validation` run'ları (`b60f58d` … `417808b`) 22/22 adım yeşil.
- `dotnet build -c Release` ve `-c Debug` sıfır uyarı / sıfır hata; `consistency_audit.py` temiz;
  `plan_audit_tool.py validate` + `validate-coverage` + `verify-manifest` sıfır hata; üç `docker compose
  config` kombinasyonu geçerli.
- `GATE-V1-EXIT` kapısı `plan/GATES.md` ve `plan/v1/README.md` üzerinde kesin olarak kapalı belgelenir.

## Handoff

- GATE-V11-ENTRY
