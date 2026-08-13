# V0-GOV-062 Kapanış Kanıtı (C65)

Tarih: 2026-08-13

## Değişiklik özeti

- `plan/GATES.md`: `V0_DEFERRED_TASKS` tablosuna 30 REV satırı eklendi
  (approval date `2026-08-13`, reopen stage `V12`, named approver kanıtı);
  GATE-V0-EXIT koşulu ve açıklama paragrafı C65'e atıfla güncellendi.
- `tools/plan-audit/plan_audit_tool.py`: `V0_DEFERRED_TASKS` sabiti 41 kimliğe
  genişletildi; deferral satır regex'i `2026-08-03|2026-08-13` kabul edecek
  şekilde genişletildi.
- `tools/task-scope/task_scope_tool.py`: `_DEFERRED_TASKS_ROW` approval date
  alternatifi ve `_DEFERRED_TASK_RECORDS` sabiti 41 kayda genişletildi.
- `tests/Architecture/TaskScope/test_task_scope.py`: `DEFERRED_TASK_IDS` ve
  `DEFERRED_ROWS` 41 kimliğe genişletildi (30 REV satırı `2026-08-13`).
- `plan/TRACEABILITY.md`: C65 kaydı eklendi.
- `plan/VALIDATION_CONTRACT.md`: deferral listesi metni C65 + 41 kimlik
  referansına güncellendi.
- `plan/v0/governance/V0-GOV-058-dynamic-route-parity.md`: test_task_scope.py
  sahipliğinin V0-GOV-062'ye devir kaydı (C65, yalnız daraltma).
- `plan/v1/identity-authorization/V1-IAM-008-authorization-linearization.md`:
  Blocker bölümü C65 sonrası duruma güncellendi (REV deferral tamam; kalan
  engel V0-GOV-041/042).

## V0-GOV-041/042 durumu

Defer edilmedi (kullanıcı kararı). Kanıtları:
- V0-GOV-041: successful candidate workflow URL/SHA + repository admin
  readback (branch protection/required-check okuma-yazma yetkisi).
- V0-GOV-042: coverage threshold, project scope ve supported exporter için
  tarihli named technical approval.

## Komut kanıtları

### plan validator

```
python -B tools/plan-audit/plan_audit_tool.py validate
Validation errors: 0
Validation warnings: 0
exit=0
```

Tam çıktı: `evidence/V0-GOV-062/plan-validate.txt`

### TaskScope + PlanAudit testleri

```
py -m pytest tests/Architecture/TaskScope tests/Architecture/PlanAudit -q
152 passed in 221.36s
exit=0
```

Tam çıktı: `evidence/V0-GOV-062/tests.txt`

## Scope notu

`task_scope_tool.py --task-id V0-GOV-062` Done statüde çalışmadığından
(araç Planned/InProgress bekler) scope denetimi doğrudan çalıştırılamadı.
Allowlist kontrolü git diff ile manuel yapıldı: değişen yolların tamamı
Owned surface içindedir. V0-GOV-058/V1-IAM-008 uncommitted değişiklikleri
başlangıç snapshot'ında mevcuttu ve bu görevin diff'ine girmedi
(RoleManagementService* yolları V1-IAM-008 yüzeyidir).
