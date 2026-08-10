# V0-GOV-028 - Control candidate code remediation

- Task ID: V0-GOV-028
- Status: Done
- Assignee: /root
- Work type: implementation
- Surface state: Existing

## Source basis

- CORR:C31
- CORR:C32

## Goal

V0 kapısı açıkken yalnız bağımsız denetimde kanıtlanan mevcut candidate-code kusurlarının ayrı, sıralı ve
fail-closed remediation görevleriyle düzeltilmesini sağlamak.

## Owned surface

- `plan/v0/governance/V0-GOV-003-remediation-execution-control.md`
- `plan/v0/governance/V0-GOV-028-candidate-code-remediation-control.md`
- `tools/task-scope/task_scope_tool.py`
- test_task_scope.py sahipliği V0-GOV-031'e devredilmiştir (C43); bu görev artık bu path'i yazamaz.
- `tests/Architecture/TaskScope/test_task_scope_remediation_exceptions.py`
- `docs/engineering/task-scope-contract.md`
- `plan/GATES.md`
- `plan/VALIDATION_CONTRACT.md`
- `plan/TRACEABILITY.md`
- `evidence/V0-GOV-028/**`

## In scope

- Kullanıcı tarafından onaylanan, kanıt kimlikli mevcut candidate-code remediation görevlerini tam kimlikle kaydetmek.
- Her istisnanın yalnız mevcut kusuru düzeltebildiğini; yeni ürün davranışı, dependency kapanışı veya gate kanıtı
  üretemediğini task-scope aracında fail-closed doğrulamak.
- İstisna dışı her görevin açık entry gate veya açık dependency nedeniyle reddedildiğini otomatik testlerle korumak.

## Out of scope

- V0 task durumunu kapatmak, yeni ürün davranışı eklemek, provider entegrasyonu yazmak veya genel gate bypass üretmek.

## Dependencies

- V0-GOV-003

## Deliverables

- Tam kimlikli candidate-code remediation izin kaydı, fail-closed parser ve ret/izin testleri.
- Her izinli görevin yalnız mevcut denetim bulgusunu düzeltebildiğini belirten yürütme sözleşmesi.

## Acceptance evidence

- Kayıtlı candidate-code remediation görevi yalnız kendi allowlist'iyle preflight'ı geçer; yeni özellik yüzeyi geçemez.
- Kayıtsız görev, bozuk kayıt veya kapsam dışı değişiklik non-zero exit ile reddedilir.
- Plan validator, task-scope testleri ve allowlist doğrulaması exit code `0` verir.

## Handoff

- V1-FND-001
- V1-FND-002
- V1-FND-004
- V1-FND-005
- V1-FND-006
- V1-IAM-004
- V1-SEC-003
