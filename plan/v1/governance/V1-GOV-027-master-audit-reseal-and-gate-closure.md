# V1-GOV-027 - Master audit reseal and gate closure

- Task ID: V1-GOV-027
- Status: InProgress
- Assignee: a04804b8-a15d-498a-9753-7b7c3a0e27b3
- Work type: validation
- Surface state: Planned

## Source basis

- PO:2026-08-29

## Goal

Tüm 5 kurtarma görevi tamamlandıktan sonra bağımsız denetimi çalıştırmak; `AUDIT_MANIFEST.json` ve `AUDIT_REPORT.md` dosyalarını yeniden üretip mühürlemek; görev sayılarını, hash'leri ve kanıtları doğrulayarak `GATE-V1-EXIT` kapısını kesin olarak kapatmak.

## Owned surface

- `plan/v1/governance/V1-GOV-027-master-audit-reseal-and-gate-closure.md`
- `plan/AUDIT_MANIFEST.json`
- `plan/AUDIT_REPORT.md`
- `plan/GATES.md`
- `plan/v1/README.md`
- `build/provenance/**`
- `evidence/V1-GOV-027/**`

## In scope

- Tüm testleri, TypeScript tip denetimini ve `--warnaserror` build'i doğrulamak.
- Audit manifestini ve raporunu sıfır hata ile güncellemek.
- `GATE-V1-EXIT` kapısını kesin olarak mühürlemek.

## Out of scope

- Application veya test kodunu değiştirmek.

## Dependencies

- V1-RMD-048

## Deliverables

- Mühürlü manifest ve kapalı `GATE-V1-EXIT`.

## Acceptance evidence

- `plan_audit_tool.py validate`, `verify-manifest` ve test matrisleri sıfır hata verir.
- `GATE-V1-EXIT` tüm gerçek kanıtlarla `Done` mühürlenir.

## Handoff

- GATE-V11-ENTRY
