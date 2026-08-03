# V0-GOV-022 - Allow legal Blocker transitions

- Task ID: V0-GOV-022
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: validation
- Surface state: Existing

## Source basis

- CORR:C37

## Goal

Bir görev `Blocked` ile `Planned` veya `InProgress` arasında geçerken yalnız kendi
zorunlu `Blocker` bölümünü ekleme veya silme işlemini, write boundary genişletmeden
ve fail-closed doğrulanabilir biçimde izinli kılmak.

## Owned surface

- `AGENTS.md`
- `tools/task-scope/task_scope_tool.py`
- `docs/engineering/task-scope-contract.md`
- `plan/TASK_STANDARD.md`
- `plan/VALIDATION_CONTRACT.md`
- `plan/TRACEABILITY.md`
- `evidence/V0-GOV-022/**`

## In scope

- Sadece aktif görev Markdown'ındaki `Blocker` bölümünün status geçişine bağlı
  dar istisnası, bunun mekanik kontrolü ve kanıtı.

## Out of scope

- Owned surface, Goal, dependency, teslimat veya başka task gövdesini değiştirme;
  product kodu, migration, dış sağlayıcı davranışı ve version gate kapatılması.

## Dependencies

- V0-GOV-017
- V0-GOV-021

## Deliverables

- `Blocked` status geçişinde yalnız zorunlu `Blocker` bölümünü ekleyen veya silen,
  diğer bütün görev Markdown değişikliklerini fail-closed reddeden scope contract.

## Acceptance evidence

- `Blocked`ten `InProgress`e geçişte yalnız `Status`, `Assignee` ve `Blocker`
  bölümünün silinmesi kabul edilir.
- Aynı geçişte `Owned surface` veya başka task gövdesi değişirse scope kontrolü
  non-zero exit verir.
- `InProgress`ten `Blocked`a geçişte yalnız `Status`, `Assignee` ve doğrulanabilir
  `Blocker` bölümünün eklenmesi kabul edilir.

## Handoff

- V0-GOV-018
