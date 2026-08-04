# V0-GOV-030 - Regenerate gate closure evidence counts

- Task ID: V0-GOV-030
- Status: Planned
- Assignee: opencode-v0-gov-030
- Work type: documentation
- Surface state: Existing

## Source basis

- CORR:C42

## Goal

`GATE-V0-EXIT` kapanış evidence'ındaki task sayımını güncel plan
durumuyla birebir eşitlemek ve Open/Closed kararını tek kaynağa bağlamak.

## Owned surface

- `evidence/v0/gate-v0-exit-closure.md`
- `plan/AUDIT_REPORT.md` (yalnız araç yeniden üretimi)
- `plan/AUDIT_MANIFEST.json` (yalnız araç yeniden üretimi)
- `evidence/V0-GOV-030/**`

## In scope

- V0 task sayımını güncel durumdan (66 task, 55 `Done`, 11 `Blocked`)
  yeniden üretmek; `51 Done / 62 task` sayımını tarihsel hata kaydına
  işlemek.
- Kapanış kararının tek kaynağını (`plan/GATES.md` C41/C42 kayıtları)
  doğrulamak ve evidence içinde referanslamak.
- Audit report ve manifesti araçla yeniden üretmek.

## Out of scope

- Gate kapanış kararının değiştirilmesi (kapanış geçerlidir), task status
  değişikliği, `plan/GATES.md` veya `plan/TRACEABILITY.md` içerik
  değişikliği (C42 plan değişikliği zaten kayıtlıdır).

## Dependencies

- V0-GOV-029

## Deliverables

- Güncel sayımlı gate closure evidence ve yeniden üretilmiş audit
  report/manifest.
- Komut, exit code ve sonuç içeren kanıt kaydı.

## Acceptance evidence

- `plan_audit_tool.py validate`, `validate-coverage` ve `verify-manifest`
  exit code `0` verir.
- Evidence sayımı gerçek plan durumuyla (66/55/11) eşittir; 51/62 sayımı
  tarihsel hata kaydında yer alır; kapanış kararı tek kaynağa (C41/C42)
  bağlanmıştır.

## Handoff

- V1-FND-001
