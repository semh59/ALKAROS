# V1-GOV-072 - Wave 25 master audit reseal and gate closure

- Task ID: V1-GOV-072
- Status: Planned
- Assignee: Unassigned
- Work type: validation
- Surface state: Planned

## Source basis

- PO:2026-09-04

## Goal

`V1-IAM-025` (25. dalga sağlamlaştırma + istemci tarafı kablolama) tamamlandıktan
sonra tüm test süitlerinin (C#, Vitest, pytest), tutarlılık denetim betiğinin,
plan bütünlüğünün ve manifest hash'lerinin doğrulanması ve `GATE-V1-EXIT`
kapısının 25. dalga (`V1-IAM-016..025`) için kesin olarak yeniden
mühürlenmesi.

## Owned surface

- `plan/v1/governance/V1-GOV-072-wave25-master-audit-reseal-and-gate-closure.md`
- `plan/AUDIT_MANIFEST.json`
- `plan/AUDIT_REPORT.md`
- `plan/GATES.md`
- `plan/v1/README.md`
- `evidence/V1-GOV-072/**`
- Bu görev, başka bir task'in owned surface alanını değiştiremez.

## In scope

- `dotnet build -c Release` ve `-c Debug` sıfır uyarı / sıfır hata; tam
  `dotnet test ALKAROS.slnx` (yerel Postgres 18).
- `python tools/consistency-audit/consistency_audit.py` sıfır hata.
- `python tools/plan-audit/plan_audit_tool.py validate`, `validate-coverage`
  ve `verify-manifest` sıfır hata; `plan/AUDIT_REPORT.md` ve
  `plan/AUDIT_MANIFEST.json` mevcut ağaç durumuna göre yeniden üretilir.
- `GATE-V1-EXIT` kapısını `plan/GATES.md` (25. dalga reseal notu) ve
  `plan/v1/README.md` (görev matrisi ve sayaç) üzerinde kesin olarak
  mühürlemek.

## Out of scope

- Production kodunda yeni özellik eklemek veya `V1-IAM-025` kapsamını
  genişletmek.

## Dependencies

- V1-IAM-025

## Deliverables

- Mühürlü `GATE-V1-EXIT` kapısı ve doğrulanmış audit manifesti.

## Acceptance evidence

- (V1-IAM-025 `Done` olduktan sonra doldurulur.)

## Handoff

- GATE-V11-ENTRY
