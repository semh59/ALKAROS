# V1-GOV-041 - Wave 9 master audit reseal and gate closure

- Task ID: V1-GOV-041
- Status: Done
- Assignee: claude-code-019uHsMymyuC1tZ5DHtmWsRi
- Work type: validation
- Surface state: Planned

## Source basis

- PO:2026-08-31

## Goal

`V1-RMD-074..077` 9. dalga kurtarma görevleri tamamlandıktan ve `V1-RMD-078` gerekçeli `NotApplicable` kapandıktan sonra tüm test süitlerinin (C#, Vitest, pytest), migration ileri/geri kanıtlarının, plan bütünlüğünün ve manifest hash'lerinin doğrulanması ve `GATE-V1-EXIT` kapısının kesin olarak yeniden mühürlenmesi.

## Owned surface

- `plan/v1/governance/V1-GOV-041-wave9-master-audit-reseal-and-gate-closure.md`
- `plan/AUDIT_MANIFEST.json`
- `plan/AUDIT_REPORT.md`
- `plan/GATES.md`
- `plan/v1/README.md`
- `evidence/V1-GOV-041/**`

## In scope

- Docker konteynerinde `dotnet test ALKAROS.slnx` ile C# test süitini sıfır hata ile çalıştırmak.
- `pnpm --dir src/Clients/PosTerminal test`, `typecheck` ve `build` komutlarını sıfır çıkış kodu ile çalıştırmak.
- `python -m pytest tests/Architecture` komutunu sıfır hata ile çalıştırmak.
- `040` katalog kullanılabilirlik migration'ının boş veritabanında ileri ve geri uygulandığını doğrulamak.
- `python tools/plan-audit/plan_audit_tool.py validate` ve `verify-manifest` komutlarını sıfır hata ile çalıştırmak; manifest hash'lerini yeniden üretmek.
- `V1-RMD-078` sonucunun gerekçeli `NotApplicable` olduğunu ve kalan davranışın (mevcut optimistic-locking "yenile" akışı) korunduğunu kayıt altına almak.
- `GATE-V1-EXIT` kapısını `plan/GATES.md` ve `plan/v1/README.md` üzerinde kesin olarak mühürlemek.

## Out of scope

- Production kodunda yeni özellik eklemek veya kurtarma görevlerinin kapsamını genişletmek.

## Dependencies

- V1-RMD-077

## Deliverables

- Mühürlü `GATE-V1-EXIT` kapısı ve doğrulanmış audit manifesti.

## Acceptance evidence

- `dotnet test`, `pnpm test`, `python -m pytest tests/Architecture`, `plan_audit_tool.py validate` ve `verify-manifest` sıfır hata verir.
- `V1-RMD-078` gerekçeli `NotApplicable` olarak ele alınır; masa çakışma davranışı için ek uygulama yapılmaz.
- `GATE-V1-EXIT` kapısı `plan/GATES.md` ve `plan/v1/README.md` üzerinde kesin olarak kapalı belgelenir.

## Handoff

- GATE-V11-ENTRY
