# V0-GOV-062 - Defer V0-REV-001..030 decision revalidations from V0 entry gate

- Task ID: V0-GOV-062
- Status: Done
- Assignee: opencode-v0-gov-062
- Work type: implementation
- Surface state: Existing

## Source basis

- CORR:C65

## Goal

`V0-REV-001..030` karar revalidation görevleri, kullanıcı onayıyla (2026-08-13,
`TRACEABILITY.md` C65) C40 desenine uygun şekilde `V0_DEFERRED_TASKS` listesine
alınır: 30 görev `plan/GATES.md` `V0_DEFERRED_TASKS` tablosuna ve
`tools/plan-audit/plan_audit_tool.py` `V0_DEFERRED_TASKS` sabitine eklenir;
böylece `GATE-V0-EXIT` türetilmiş entry-gate kontrolü bu görevleri kapanma
koşulundan muaf sayar. `V0-GOV-041`/`V0-GOV-042` deferred değildir; kullanıcı
kanıtı sağlayana kadar V1 application admission engeli olmaya devam eder.

## Owned surface

- `plan/GATES.md` (yalnız `V0_DEFERRED_TASKS` tablo bölümü)
- `tools/plan-audit/plan_audit_tool.py` (yalnız `V0_DEFERRED_TASKS` sabiti ve
  deferral satır regex'i)
- `tools/task-scope/task_scope_tool.py` (yalnız `_DEFERRED_TASKS_ROW` regex'i
  ve `_DEFERRED_TASK_RECORDS` sabiti)
- `tests/Architecture/TaskScope/test_task_scope.py`
- `plan/v0/governance/V0-GOV-058-dynamic-route-parity.md` (yalnız
  test_task_scope.py sahiplik devir kaydı, C65)
- `plan/v1/identity-authorization/V1-IAM-008-authorization-linearization.md`
  (yalnız Blocker bölümünün C65 sonrası duruma güncellenmesi)
- `plan/TRACEABILITY.md` (yalnız C65 kaydı)
- `plan/VALIDATION_CONTRACT.md` (yalnız V0 deferral listesi ile ilgili metin)
- `evidence/V0-GOV-062/**`

## In scope

- `V0-REV-001..030` kimliklerini `V0_DEFERRED_TASKS` sabitine ve GATES.md
  marker tablosuna eklemek; her satır için `Reopen stage` = `V12` (exit sonrası
  uygulama aşaması), `Required evidence` = tarihli source packet + named
  approver (ad-soyad, kurum/rol, onay tarihi), `Gate closure evidence` = Not V0
  gate closure evidence.
- `plan_audit_tool.py` deferral satır regex'ini approval date
  `2026-08-03`/`2026-08-13` kabul edecek şekilde genişletmek (yeni satırlar
  `2026-08-13` onay tarihi taşır; eski 11 satır değişmez).
- TaskScope testlerinde `DEFERRED_TASK_IDS` / `DEFERRED_ROWS` sabitlerini 41
  kimliğe güncellemek; mevcut fail-closed testlerin (satır bozulması, kimlik
  uyuşmazlığı, tablo yokluğu) yeni setle de geçtiğini doğrulamak.
- `TRACEABILITY.md` C65 kaydı: 2026-08-13 kullanıcı onaylı plan değişikliği,
  onay içeriği, kapsam dışı bırakılan V0-GOV-041/042 kararı.
- `VALIDATION_CONTRACT.md` deferral ile ilgili metin güncellemesi.

## Out of scope

- `V0-GOV-041` ve `V0-GOV-042` defer edilmez; bu görevler kullanıcının
  sağlayacağı gerçek kanıtla (workflow URL/SHA + admin readback; coverage named
  approval) kendi görevlerinde kapanır.
- V0 görev durumlarını değiştirmek, gate kapanış kararını değiştirmek,
  `TASK_SCOPE_REMEDIATION_EXCEPTIONS` / onay seti mekanizmasını değiştirmek,
  yeni ürün davranışı eklemek veya gate kapanış kanıtı üretmek.
- `V0-GOV-058` uncommitted çalışması (ORIGIN_FINDING_IDS bloğu) korunur;
  bu bloğa dokunulmaz.

## Dependencies

- V0-GOV-032
- V0-GOV-035

## Deliverables

- 30 REV kimlikli güncel `V0_DEFERRED_TASKS` sabiti + GATES.md tablosu +
  güncel TaskScope testleri + C65 kaydı; komut, exit code ve sonuç içeren kanıt
  kaydı.

## Acceptance evidence

- `python -B tools/plan-audit/plan_audit_tool.py validate` exit code `0`
  (`GATES_V0_DEFERRED_MISMATCH` yok).
- `py -m pytest tests/Architecture/TaskScope -q` exit code `0`.
- `py -m pytest tests/Architecture/PlanAudit -q` exit code `0` (varsa).
- `V0-REV-001..030` deferral listesinde olduğu için artık
  `APPLICATION_STARTED_BEFORE_V0_EXIT` nedeni sayılmaz; kalan V1 admission
  engeli yalnız `V0-GOV-041`/`V0-GOV-042` blocker'larından kaynaklanır
  (validator çıktısıyla doğrulanır).
- Komut, exit code ve sonuç `evidence/V0-GOV-062/**` altında kayıtlıdır.

## Handoff

- V1-IAM-008
- V0-GOV-036
