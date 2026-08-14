# V0-GOV-063 - Defer V0-GOV-041/042 integration gates (C66)

- Task ID: V0-GOV-063
- Status: InProgress
- Assignee: opencode-v0-gov-063
- Work type: plan-change
- Surface state: Existing

## Source basis

- CORR:C66

## Goal

`V0-GOV-041` (repository verification workflow + required check) ve `V0-GOV-042`
(coverage gate) görevlerinin dış entegrasyon kanıtını (GitHub branch
policy/ruleset readback; coverage için tarihli named technical approval) V0
exit'ten sonraya devretmek. Her iki görev de kullanıcı kararı ve GitHub
admin/onay kanıtı gerektirir; bu kanıt V0 gate kapanışından önce
üretilemez/doğrulanamaz.

## Owned surface

- `plan/GATES.md`
- `plan/TRACEABILITY.md`
- `plan/VALIDATION_CONTRACT.md`
- `tools/plan-audit/plan_audit_tool.py`
- `tools/task-scope/task_scope_tool.py`
- `tests/Architecture/TaskScope/test_task_scope.py`
- `tests/Architecture/PlanAudit/test_plan_audit.py`
- `plan/v1/identity-authorization/V1-IAM-008-authorization-linearization.md`
- `evidence/V0-GOV-063/**`

## In scope

- `V0_DEFERRED_TASKS` setine `V0-GOV-041`, `V0-GOV-042` eklemek (43 kimlik).
- `GATES.md` `V0_DEFERRED_TASKS` tablosuna 2026-08-13 onay tarihli iki satır
  eklemek (reopen stage V12, named approver kanıtı).
- `TRACEABILITY.md` C66 kaydını yazmak.
- `VALIDATION_CONTRACT.md` deferral sayısını 41 -> 43 güncellemek.
- `task_scope_tool.py` `_DEFERRED_TASK_RECORDS` ve `test_task_scope.py`
  `DEFERRED_TASK_IDS`/`DEFERRED_ROWS` setlerini 43 kimliğe genişletmek.
- `V1-IAM-008` Blocker bölümünü C66 sonrası duruma güncellemek (GOV-041/042
  artık defer edildi; kalan engel yalnız kullanıcı onaylı devir kaydının
  varlığı).
- `plan_audit_tool.py` `application_tasks_started_before_v0_exit` davranışını
  C66 sonrasına uyarlamak: tüm Blocked V0 görevleri deferred olduğunda gate
  fiilen kapalıdır; V1-FND-023 C54 admission deseni ve
  `validate_remediation_admission_tuple` doğrulaması her durumda çalışır
  (fail-closed), `APPLICATION_STARTED_BEFORE_V0_EXIT` yalnız gerçek Blocked
  V0 görevi kaldığında üretilir (kullanıcı onaylı kapsam düzenlemesi,
  2026-08-14).
- `test_plan_audit.py` C54 admission testlerini C66 sonrası davranışa
  güncellemek: divergence testleri C54 desen hatasını bekler;
  `test_other_v1_application_is_not_admitted` gate'i açık simüle etmek için
  deferred olmayan bir V0 görevini Planlı -> Blocked yapar (kullanıcı onaylı
  kapsam düzenlemesi, 2026-08-14).

## Out of scope

- `V0-GOV-041`/`V0-GOV-042` görev dosyalarının status/assignee/acceptance
  bölümlerini değiştirmek (historical task'lar aynen kalır, yalnız devir
  kaydı TRACEABILITY/GATES'te yazılır).
- Coverage threshold, ruleset içeriği veya workflow davranışı uydurmak;
  GitHub branch policy kanıtı üretmek.
- Başka görevin production yüzeyi, testi veya plan dosyasını değiştirmek.

## Dependencies

- V0-GOV-062
- V0-GOV-032
- V0-GOV-035

## Deliverables

- 43 kimlikli deferral listesi (GATES + araç + test), C66 kaydı ve
  V1-IAM-008 Blocker güncellemesi; validator ve test kanıtları.

## Acceptance evidence

- `python -B tools/plan-audit/plan_audit_tool.py validate` exit code `0`
  verir (GATES tablosu araç setiyle 43 kimlik birebir eşleşir).
- TaskScope + PlanAudit testleri exit code `0` verir (43 kimlik) ve
  `task_scope_tool.py` deferral satır regex'i `2026-08-13` onay tarihli
  GOV-041/042 satırlarını kabul eder.
- `V0-GOV-041` ve `V0-GOV-042` deferral satırları GATES.md'de `2026-08-13`
  onay tarihi ve `V12` reopen stage ile kayıtlıdır.
- `TRACEABILITY.md` C66 kaydı ve `VALIDATION_CONTRACT.md` 43 kimlik metni
  mevcuttur; `git diff --check` temizdir.
- C54 admission: deferred olmayan Blocked V0 görevi varken
  `APPLICATION_STARTED_BEFORE_V0_EXIT` üretilir; tüm Blocked V0 görevleri
  deferred iken C54 desen/tuple hataları yine yakalanır (fail-closed).

## Handoff

- V1-IAM-008
- V0-GOV-045
