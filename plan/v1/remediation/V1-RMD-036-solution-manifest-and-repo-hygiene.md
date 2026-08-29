# V1-RMD-036 - Solution manifest synchronization and repo hygiene

- Task ID: V1-RMD-036
- Status: Planned
- Assignee: Unassigned
- Work type: implementation
- Surface state: Planned

## Goal

`ALKAROS.slnx` ve disk üzerindeki test projeleri arasındaki drift'i (`ALKAROS.Host.Experience.Billing.Tests.csproj`) gidermek, `build/project-manifest.json` bütünlüğünü sağlamak ve root dizindeki geçici çıktı dosyalarını depodan temizleyerek `.gitignore`'a eklemek.

## Owned surface

- `plan/v1/remediation/V1-RMD-036-solution-manifest-and-repo-hygiene.md`
- `ALKAROS.slnx`
- `build/project-manifest.json`
- `.gitignore`
- `output.txt`
- `output2.txt`
- `test_output.txt`
- `evidence/V1-RMD-036/**`

## In scope

- `tests/Host/Experience/Billing/ALKAROS.Host.Experience.Billing.Tests.csproj` projesini `ALKAROS.slnx` çözüm dosyasına eklemek.
- `build/project-manifest.json` manifestini çözümle senkronize etmek ve `project_manifest_tool.py` doğrulamasını yeşile çekmek.
- `output.txt`, `output2.txt` ve `test_output.txt` dosyalarını git takibinden çıkarıp `.gitignore` kurallarını güncellemek.
- Doğrulama kanıtlarını `evidence/V1-RMD-036/**` altında kaydetmek.

## Out of scope

- C# iş mantığı veya test kodu değiştirmek.
- Diğer plan veya mimari kurallarını değiştirmek.

## Dependencies

- V1-RMD-035

## Acceptance evidence

- `python tools/project-manifest/project_manifest_tool.py` exit code `0` verir.
- `python -m pytest tests/Architecture/ProjectManifest/test_project_manifest.py` exit code `0` verir.
- `python tools/plan-audit/plan_audit_tool.py validate` 0 hata, 0 uyarı verir.
- Git çalışma ağacı temizdir; geçici log dosyaları git takibinden çıkarılmıştır.

## Handoff

- V1-RMD-006
