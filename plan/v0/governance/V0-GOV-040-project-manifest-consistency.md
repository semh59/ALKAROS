# V0-GOV-040 - Make the project manifest exact

- Task ID: V0-GOV-040
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation
- Surface state: Existing

## Source basis

- CORR:C52

## Goal

`V1-FND-001` tarafından yenilenen project manifest ile solution, disk, project reference, package ve lock graph'ının exact eşitliğini fail-closed doğrulamak.

## Owned surface

- `tools/project-manifest/project_manifest_tool.py`
- `tests/Architecture/ProjectManifest/test_project_manifest.py`
- `evidence/V0-GOV-040/**`

## In scope

- Final Tables import sonrasında solution/disk/project manifest exact setlerini read-only karşılaştırmak.
- Locked restore ve Release build'i manifest candidate SHA'sıyla kanıtlamak.

## Out of scope

- Eksik projeyi manifestten gizlemek ya da restore'u unlocked çalıştırarak lock drift'i kabul etmek.
- Solution, csproj, package lock veya ürün source dosyası değiştirmek; bu reserved surface V1-FND-001'de kalır.

## Dependencies

- V0-GOV-035
- V1-FND-001
- V1-FND-023
- V1-FND-022

## Deliverables

- Fail-closed project-manifest validator, positive/negative fixtures ve raw transcript.

## Acceptance evidence

- Solution, disk ve manifest project set farkları `0` olur.
- ProjectReference/package/lock graph drift fixture'ları non-zero reddedilir.
- `dotnet restore ALKAROS.slnx --locked-mode` ve locked Release build exit code `0` verir.
- `python -B tools/plan-audit/plan_audit_tool.py validate` exit code `0` verir; kanıtlar yalnız `evidence/V0-GOV-040/**` altındadır.

## Handoff

- V1-CAT-004
- V0-GOV-045
