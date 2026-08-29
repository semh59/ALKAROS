# V1-GOV-029 - Master audit reseal and gate closure

- Task ID: V1-GOV-029
- Status: Planned
- Assignee: Unassigned
- Work type: validation
- Surface state: Planned

## Source basis

- PO:2026-08-29

## Goal

Tüm kurtarma görevleri (`V1-RMD-049..052`) başarıyla tamamlandıktan sonra, tüm mimari testlerin, istemci sözleşmelerinin, plan bütünlüğünün ve manifest hash'lerinin doğrulanması ve `GATE-V1-EXIT` kapısının kesin olarak yeniden mühürlenmesi.

## Owned surface

- `plan/v1/governance/V1-GOV-029-master-audit-reseal-and-gate-closure.md`
- `plan/AUDIT_MANIFEST.json`
- `plan/AUDIT_REPORT.md`
- `plan/GATES.md`
- `plan/v1/README.md`
- `build/provenance/**`
- `evidence/V1-GOV-029/**`

## In scope

- `plan_audit_tool.py` ile plan doğrulama ve manifest hash üretimini sıfır hata ile gerçekleştirmek.
- `GATE-V1-EXIT` kapısını `plan/GATES.md` ve `plan/v1/README.md` üzerinde kesin olarak mühürlemek.
- `build/provenance` manifestini güncellemek ve kanıt paketini `evidence/V1-GOV-029/` altında üretmek.

## Out of scope

- Production kodunda yeni özellik eklemek.

## Dependencies

- V1-RMD-052

## Deliverables

- Mühürlü `GATE-V1-EXIT` kapısı ve doğrulanmış audit/provenance manifestleri.

## Acceptance evidence

- `python tools/plan-audit/plan_audit_tool.py validate` ve `verify-manifest` 0 hata verir.
- `GATE-V1-EXIT` kapısı kesin olarak kapalıdır.

## Handoff

- GATE-V11-ENTRY
