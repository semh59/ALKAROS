# V0-GOV-010 - Normalize task-scope repository roots

- Task ID: V0-GOV-010
- Status: Done
- Assignee: codex-v0-gov-010
- Work type: implementation
- Surface state: Existing

## Source basis

- CORR:C37

## Goal

Task-scope denetleyicisinin goreli veya mutlak repository root girdisinde ayni
sonucu uretmesini ve traceback yerine fail-closed JSON sonucu vermesini saglamak.

## Owned surface

- `tools/task-scope/task_scope_tool.py`
- `tests/Architecture/TaskScope/test_task_scope_root_path.py`
- `evidence/V0-GOV-010/**`
- Bu gorev, baska bir task'in owned surface alanini degistiremez.

## In scope

- Repository root ve plan directory girdilerini mutlak, normalize edilmis Path
  degerlerine donusturmek.
- Goreli root regression testi ve mevcut task-scope acceptance komutlari.

## Out of scope

- Task metadata kurallari, allowlist semantigi, product code, gate sonucu veya
  baska bir gorevin owned surface alanini degistirmek.

## Dependencies

- V0-GOV-009
- V1-FND-003

## Deliverables

- Goreli ve mutlak root girdilerinde ayni JSON contract'ini uretecek task-scope
  duzeltmesi ve regression testi.
- `evidence/V0-GOV-010/**` altinda komut, exit code ve sonuc kaydi.

## Acceptance evidence

- Goreli `--repo-root .` ve mutlak root ile calisan CLI traceback vermez;
  metadata sonucu ayni olur.
- `py -m pytest tests/Architecture/TaskScope -q` exit code `0` verir.
- Goreli root, mutlak root veya plan directory ile `..` traversal izni uretmez.

## Handoff

- GATE-V0-EXIT
