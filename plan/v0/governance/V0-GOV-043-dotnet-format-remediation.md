# V0-GOV-043 - Close the dotnet-format failure

- Task ID: V0-GOV-043
- Status: Done
- Assignee: Antigravity-v0-gov-043
- Work type: validation
- Surface state: Existing

## Source basis

- CORR:C52

## Goal

`V1-IAM-008` tarafından düzeltilen exact C# formatting farkından sonra zorunlu `dotnet format --verify-no-changes`
kapısını bağımsız doğrulamak.

## Owned surface

- `evidence/V0-GOV-043/**`

## In scope

- Formatter diagnostic'inin işaret ettiği exact whitespace/format farkını read-only diff ile doğrulamak.
- Dosyanın semantic davranışının değişmediğini doğrulamak.
- Repository-wide formatter sonucunu raw exit code ile kaydetmek.

## Out of scope

- Unrelated refactor, rename, cleanup veya production logic değiştirmek.
- Analyzer suppression, formatter disable veya config gevşetmesi eklemek.

## Dependencies

- V0-GOV-035
- V1-IAM-008

## Deliverables

- Tek dosyalı mechanical format düzeltmesinin bağımsız verification transcript'i.

## Acceptance evidence

- `V1-IAM-008` candidate diff'inde yalnız formatter kaynaklı değişiklik bulunur.
- `dotnet format ALKAROS.slnx --verify-no-changes --no-restore` exit code `0` verir.
- İlgili Authorization testleri exit code `0` verir.
- `python -B tools/plan-audit/plan_audit_tool.py validate` exit code `0` verir; kanıtlar yalnız `evidence/V0-GOV-043/**`
  altındadır.

## Handoff

- V0-GOV-041
