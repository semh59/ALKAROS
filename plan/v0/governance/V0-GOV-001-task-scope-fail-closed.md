# V0-GOV-001 - Harden task-scope enforcement

- Task ID: V0-GOV-001
- Status: Done
- Assignee: /root
- Work type: implementation
- Surface state: Existing

## Source basis

- CORR:C31

## Goal

Task Markdown'ının kendi write allowlist'ini genişletmesini ve `Done` veya gate'i
kapalı görevlerin uygulama yazmasını fail-closed olarak engellemek.

## Owned surface

- `tools/task-scope/task_scope_tool.py`
- `tests/Architecture/TaskScope/test_task_scope_markdown_boundary.py`
- `docs/engineering/task-scope-contract.md`
- `evidence/V0-GOV-001/**`

## In scope

- Yalnız `Status` ve `Assignee` metadata satırlarına izin veren task Markdown
  diff denetimi.
- `Planned` status, tamamlanmış dependency ve gate zinciri kontrolü.
- Scope metninde path görünen serbest açıklamaların allowlist sayılmaması.

## Out of scope

- Uygulama iş mantığı, mevcut task scope'larını genişletmek veya geçmiş commit'i
  yeniden yazmak.

## Dependencies

- None

## Deliverables

- Local ve CI diff modlarında aynı bulguyu üreten fail-closed scope denetimi.
- Self-escalation, `Done` task, açık dependency ve gate bypass için otomatik
  ret testleri.

## Acceptance evidence

- `Owned surface` veya başka task alanını değiştiren task Markdown diff'i non-zero
  exit verir.
- `Done`, `Blocked`, `NotApplicable` veya entry gate'i kapanmamış task için
  production/evidence olmayan write-set non-zero exit verir.
- Geçerli `Planned` task yalnız own surface, iki metadata satırı ve own evidence
  altında exit code 0 üretir.

## Handoff

- GATE-V0-EXIT
