# V13-GOV-008 - Waive the per-task Dependencies-Done requirement for 8 payment orchestration tasks

- Task ID: V13-GOV-008
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: decision
- Surface state: Existing

## Source basis

- PO:2026-09-23

## Goal

`V14-GOV-002` (2026-09-22) yalnız `GATE-V13-EXIT`/`GATE-V14-ENTRY` release-gate
zincirini esnetmişti — v1.3'ün kendi 14 dış-sözleşme görevi
(`V13-FSC-001..004`, `V13-HUG-001..004`, `V13-MCD-001..004`, `V13-ALC-004`,
`V13-PUI-003`) açık kaldığı sürece v1.4'ün BAŞLAMASINI engellememesini
sağlamıştı. Ama v1.3'ün kendi içinde ayrı bir sekizli görev grubu
(`V13-PAY-003/004/005`, `V13-PUI-001/004`, `V13-TBL-001`, `V13-REC-001`,
`V13-RPT-001`) — bu 14 görevi kendi `## Dependencies` bölümlerinde DOĞRUDAN
listeliyor. `tools/plan-audit/plan_audit_tool.py`'nin `DONE_DEPENDENCY_NOT_
FINAL`/`DONE_DEPENDENCY_TRANSITIVE_NOT_FINAL` kontrolü (kapanış gate'i,
AGENTS.md'nin zorunlu koştuğu `plan_audit_tool.py validate` bunu çalıştırır)
bu sekiz görevin HİÇBİRİNİ, waived 14 görevden biri gerçek kanıtla kapanmadan
`Done` yapılmasına izin vermiyordu — dolaylı olarak bu sekiz görevi de
sonsuza kadar kilitliyordu, kendi Owned surface'ları terminal protokolü/
fiscal strateji/meal-card sağlayıcısına HİÇ dokunmasa bile.

Her sekiz görevin kendi metni incelendi: hepsi zaten ZATEN Done olan tipli
bir sözleşme (`ITenderHandler`/`TenderHandlerResult`, `PaymentAllocation`,
vb. — `V13-PAY-002` sahipliğinde, Done) üzerine inşa ediliyor, ve her biri
kendi Out-of-scope bölümünde somut sağlayıcı entegrasyonunu ("Terminal
protocol", "QNB, çevrimiçi provider") açıkça hariç tutuyor. Her birinin
kendi Acceptance evidence metni de zaten "eğer X görevi NotApplicable/yoksa,
o bacak disabled/typed-unavailable kalır" davranışını tarif ediyor — yani
waived bağımlılık kapanmadan da GERÇEK, sahte olmayan bir Done durumuna
ulaşmaları mümkün. Semih 2026-09-23'te bu okumayı onayladı ve gate'in resmi
olarak esnetilmesini seçti (bkz. Onay).

## Owned surface

- `tools/plan-audit/plan_audit_tool.py`
- `tools/task-scope/task_scope_tool.py`
- `plan/GATES.md`
- `plan/TRACEABILITY.md`
- `plan/v1.3/governance/V13-GOV-008-payment-orchestration-dependency-waiver.md`
- `evidence/V13-GOV-008/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Architecture/TaskScope/test_task_scope.py
  (V1-GOV-066 sahipliğinde) — yalnız yeni `TestPaymentOrchestrationDependencyWaiver`
  sınıfı eklendi; mevcut testler değişmedi.

## In scope

1. `tools/plan-audit/plan_audit_tool.py`: yeni `PAYMENT_ORCHESTRATION_
   DEPENDENCY_WAIVER` sabiti (tam olarak 12 `(consumer, waived dependency)`
   çifti — aşağıdaki tabloyla birebir), `find_non_final_ancestors`'ın
   `len(path) == 2` (doğrudan kenar) durumunda bu kümeyi kontrol etmesi, ve
   `plan/GATES.md`'nin yeni işaretli tablosuyla çapraz doğrulama (V0 deferral
   tablosunun aynı fail-closed deseni).
2. `tools/task-scope/task_scope_tool.py`: birebir aynı 12 çift
   (`_PAYMENT_ORCHESTRATION_DEPENDENCY_WAIVER`), `validate_task_metadata`'nın
   dependency döngüsünde bu çiftleri atlaması (yalnız listelenen kenar,
   aynı tüketicinin BAŞKA bir bağımlılığını etkilemez).
3. `plan/GATES.md`: `V13_PAYMENT_ORCHESTRATION_DEPENDENCY_WAIVER` işaretli
   tablosu, 12 satır, 2026-09-23 onay tarihi.
4. `tests/Architecture/TaskScope/test_task_scope.py`:
   `TestPaymentOrchestrationDependencyWaiver` (3 test: waived kenar gate'i
   kapatmaz, aynı görevin BAŞKA bir bağımlılığı yine zorunlu kalır, waiver
   yalnız kayıtlı tüketiciye özeldir).
5. `plan/TRACEABILITY.md`'ye `C102` kaydı.

## Out of scope

- Bu 8 görevin kendisini `InProgress`/`Done` yapmak veya kod yazmak — bu
  yalnız mekanizmayı açar, uygulamayı yapmaz (her biri kendi Task ID'siyle
  ayrı ilerler).
- Waived 14 görevin (`V13-HUG-001` vb.) kendisini `Done`/`NotApplicable`
  yapmak — onlar hâlâ gerçek dış sözleşme kanıtı olmadan kapanamaz.
- `GATE-V13-EXIT`in kendisi veya `V14-GOV-002`'nin release-gate waiver'ı —
  bu tamamen ayrı, dokunulmayan bir mekanizma.

## Dependencies

- None

## Onay

Approved by Semih — Founder/Product Owner — 2026-09-23. Faz 2 (v1.3 ödeme
yan-görevleri) başlamadan önce AskUserQuestion ile sunulan 4 seçenekten
"Gerçek bağımlılık gate'ini resmi olarak esnet (Recommended)" seçildi —
V13-GOV-006/V14-GOV-002 emsaliyle, taslak/standalone değil gerçek Owned
Surface'ta, arayüz/tipli-sözleşme seviyesinde entegrasyon.

## Deliverables

- `tools/plan-audit/plan_audit_tool.py`: `PAYMENT_ORCHESTRATION_DEPENDENCY_
  WAIVER`, `find_non_final_ancestors` güncellemesi, `GATES_PAYMENT_
  ORCHESTRATION_WAIVER_MARKER_MISSING`/`_MISMATCH` çapraz doğrulaması.
- `tools/task-scope/task_scope_tool.py`: `_PAYMENT_ORCHESTRATION_DEPENDENCY_
  WAIVER`, `validate_task_metadata` güncellemesi.
- `plan/GATES.md`: `V13_PAYMENT_ORCHESTRATION_DEPENDENCY_WAIVER` tablosu.
- `tests/Architecture/TaskScope/test_task_scope.py`:
  `TestPaymentOrchestrationDependencyWaiver` (3 test).
- `plan/TRACEABILITY.md`: `C102` (başlangıçta `C101` olarak atanmıştı, önceden var olan ve alakasız
  bir `V15-GOV-001` kaydıyla çakıştığı için yeniden numaralandırıldı — Faz 2 bağımsız denetiminde
  (`V13-GOV-009`) bulunup düzeltildi).

## Acceptance evidence

- `python -m pytest tests/Architecture/TaskScope/ -q` → 113/113, 0 başarısız
  (110 önceki + 3 yeni `TestPaymentOrchestrationDependencyWaiver`).
- `python tools/plan-audit/plan_audit_tool.py validate` → gerçek `plan/`
  dizinine karşı 0 hata, 0 uyarı (896 task dosyası, 2001 dependency edge).
- Waiver tablosu `plan_audit_tool.py`/`task_scope_tool.py` sabitleriyle ve
  `plan/GATES.md`'nin işaretli bloğuyla tam eşleşiyor (üçü de birbirinden
  bağımsız ayrıştırılıp karşılaştırıldı).
- `python tools/consistency-audit/consistency_audit.py` → temiz.

## Handoff

- V13-PAY-003
- V13-PAY-004
- V13-PAY-005
- V13-PUI-001
- V13-PUI-004
- V13-TBL-001
- V13-REC-001
- V13-RPT-001
