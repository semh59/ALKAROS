# V1-GOV-055 - Wave 16 master audit reseal and gate closure

- Task ID: V1-GOV-055
- Status: Done
- Assignee: claude-code-019uHsMymyuC1tZ5DHtmWsRi
- Work type: validation
- Surface state: Planned

## Source basis

- PO:2026-09-01

## Goal

`V1-RMD-089` 16. dalga orders ölçek indeks migration görevi tamamlandıktan sonra tüm test süitlerinin (C#, Vitest, pytest), tutarlılık denetim betiğinin, plan bütünlüğünün ve manifest hash'lerinin doğrulanması ve `GATE-V1-EXIT` kapısının kesin olarak yeniden mühürlenmesi.

## Owned surface

- `plan/v1/governance/V1-GOV-055-wave16-master-audit-reseal-and-gate-closure.md`
- `plan/AUDIT_MANIFEST.json`
- `plan/AUDIT_REPORT.md`
- `plan/GATES.md`
- `plan/v1/README.md`
- `evidence/V1-GOV-055/**`

## In scope

- Docker konteynerinde `dotnet test ALKAROS.slnx` ile C# test süitini sıfır hata ile çalıştırmak; migration composition testleri dahil.
- `pnpm --dir src/Clients/PosTerminal test`, `typecheck` ve `build` komutlarını sıfır çıkış kodu ile çalıştırmak.
- `python -m pytest tests/Architecture` ve `python tools/consistency-audit/consistency_audit.py` komutlarını sıfır hata ile çalıştırmak.
- Migration `041` ileri/geri çalıştırmasını atılabilir PostgreSQL 18 üzerinde doğrulamak.
- `python tools/plan-audit/plan_audit_tool.py validate` ve `verify-manifest` komutlarını sıfır hata ile çalıştırmak; manifest hash'lerini yeniden üretmek.
- `GATE-V1-EXIT` kapısını `plan/GATES.md` ve `plan/v1/README.md` üzerinde kesin olarak mühürlemek.

## Out of scope

- Production kodunda yeni özellik eklemek veya kurtarma görevinin kapsamını genişletmek.

## Dependencies

- V1-RMD-089

## Deliverables

- Mühürlü `GATE-V1-EXIT` kapısı ve doğrulanmış audit manifesti.

## Acceptance evidence

- `dotnet test`, `pnpm test`, `python -m pytest tests/Architecture`, `consistency_audit.py`, migration `041` ileri/geri, `plan_audit_tool.py validate` ve `verify-manifest` sıfır hata verir.
- `GATE-V1-EXIT` kapısı `plan/GATES.md` ve `plan/v1/README.md` üzerinde kesin olarak kapalı belgelenir.

## Handoff

- GATE-V11-ENTRY
