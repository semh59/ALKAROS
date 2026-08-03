# V0-GOV-002 - Align task-scope test fixtures with fail-closed enforcement

- Task ID: V0-GOV-002
- Status: Done
- Assignee: /root
- Work type: implementation
- Surface state: Existing

## Source basis

- CORR:C31

## Goal

Task-scope test fixture'larını committed görev baseline'ı, önceki sürüm entry
gate'i ve `Planned` başlangıç durumu kurallarıyla uyumlu hâle getirmek.

## Owned surface

- `tests/Architecture/TaskScope/conftest.py`
- `evidence/V0-GOV-002/**`

## In scope

- Fixture'ın V0 prerequisite task'ını ve aktif task Markdown baseline'ını Git'e
  kaydetmesi.
- Eski `Planned` reddi beklentisinin yeni sözleşmeye göre düzeltilmesi.

## Out of scope

- Scope enforcement davranışını gevşetmek, production kodu veya başka test
  modüllerini değiştirmek.

## Dependencies

- V0-GOV-001

## Deliverables

- 62 task-scope testinin fail-closed sözleşmeyle uyumlu fixture ve assertion'ları.
- Komut, exit code ve test toplamını kaydeden doğrulama kanıtı.

## Acceptance evidence

- `py -m pytest tests/Architecture/TaskScope -q` exit code `0` üretir.
- Fixture, untracked görev Markdown'ını geçerli input olarak kullanmaz.
- `Planned` task, kapalı preceding gate altında geçer; `Done` task reddedilir.

## Handoff

- GATE-V0-EXIT
