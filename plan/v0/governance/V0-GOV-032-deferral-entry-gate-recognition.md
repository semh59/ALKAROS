# V0-GOV-032 - Recognize user-approved V0 deferrals in entry-gate derivation

- Task ID: V0-GOV-032
- Status: Done
- Assignee: opencode-v0-gov-032
- Work type: implementation
- Surface state: Existing

## Source basis

- CORR:C44

## Goal

`GATE-V0-EXIT` türetilmiş entry-gate kontrolünün (C41 kapanışından sonra
11 devirli V0 görevi `Blocked` kaldığı için makinece "open" çıkıyor) C40
kullanıcı onaylı devirleri tanıması: task-scope aracı `plan/GATES.md`
`V0_DEFERRED_TASKS` tablosunu fail-closed okur ve 11 devir kimliğini yalnız
`GATE-V0-EXIT` türetilmiş kontrolünün kapanma koşulundan muaf sayar. Böylece
istisna seti dışındaki V1 görevleri (V1-FND-010 dahil) remediasyon istisnası
olmadan `InProgress` olabilir; kapanış kararı hâlâ kullanıcı onaylı C41
kaydına dayanır.

## Owned surface

- `tools/task-scope/task_scope_tool.py`
- `tests/Architecture/TaskScope/test_task_scope.py`
- `docs/engineering/task-scope-contract.md`
- `plan/GATES.md`
- `plan/VALIDATION_CONTRACT.md`
- `evidence/V0-GOV-032/**`

## In scope

- `V0_DEFERRED_TASKS` tablosu ayrıştırması: `<!-- V0_DEFERRED_TASKS:START -->`
  ve `<!-- V0_DEFERRED_TASKS:END -->` marker'ları zorunlu; header/separator ve
  satır regex'i strict; eksik marker, eksik/tekrar/bozuk satır, araç tanımlı
  izinli set ile uyuşmayan kimlik → fail-closed red (tablo yoksa istisna
  uygulanmaz).
- Muafiyet yalnız `GATE-V0-EXIT` türetilmiş kontrolünde (prerequisite version
  V0) uygulanır; diğer aşama gate'lerini, `TASK_SCOPE_REMEDIATION_EXCEPTIONS`
  mekanizmasını veya `_APPROVED_REMEDIATION_TASK_IDS` kümesini değiştirmez.
- Fail-closed testler: geçerli tablo → istisna seti dışı V1 görevi geçer;
  marker/header/satır bozulması veya kimlik uyuşmazlığı → `GATE-V0-EXIT` için
  "cannot be verified" reddi; tablodan kimlik çıkarma → gate yeniden açılır.
- `docs/engineering/task-scope-contract.md` ve `plan/VALIDATION_CONTRACT.md`
  güncellemesi.

## Out of scope

- V0 görev durumlarını değiştirmek, gate kapanış kararını değiştirmek,
  `TASK_SCOPE_REMEDIATION_EXCEPTIONS` / onay seti mekanizmasını değiştirmek,
  yeni ürün davranışı eklemek veya gate kapanış kanıtı üretmek.

## Dependencies

- V0-GOV-031

## Deliverables

- Devir tanımalı `GATE-V0-EXIT` türetimi + fail-closed testler + sözleşme
  metinleri; komut, exit code ve sonuç içeren kanıt kaydı.

## Acceptance evidence

- `py -m pytest tests/Architecture/TaskScope -q` exit code `0`.
- İstisna seti dışındaki bir V1 görevi (ör. V1-FND-010) geçerli devir
  tablosuyla `OK: All changes within scope` verir; bozuk devir tablosu
  fail-closed reddeder.
- `py tools/plan-audit/plan_audit_tool.py validate` ve
  `py tools/plan-audit/plan_audit_tool.py validate-coverage` exit code `0`.
- Komut, exit code ve sonuç `evidence/V0-GOV-032/**` altında kayıtlıdır.

## Handoff

- V1-FND-010
- V1-FND-003
