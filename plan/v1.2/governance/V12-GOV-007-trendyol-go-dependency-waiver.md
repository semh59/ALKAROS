# V12-GOV-007 - Trendyol Go adaptörünün V12-TGO-001 bağımlılığına feragat

- Task ID: V12-GOV-007
- Status: Done
- Assignee: Claude Opus 5.5 (session 703155c9)
- Work type: decision
- Surface state: Existing

## Source basis

- PO:2026-09-26

## Goal

`V12-TGO-002` kendi `## Dependencies` bölümünde `Blocked` `V12-TGO-001`'e (gerçek satıcı bilgileri ve stage
erişimi) bağlı; `V12-TGO-003..005` ona zincirleniyor ve `DONE_DEPENDENCY_NOT_FINAL` kontrolü hepsini kilitliyor.
Semih 2026-09-26'da Yemeksepeti emsalindeki (`C103`, `V12-GOV-004`) gibi "feragatle taslak olarak yaz" kararını
verdi. Bu görev o tek kenara per-edge feragat kaydeder.

## Owned surface

- `plan/v1.2/governance/V12-GOV-007-trendyol-go-dependency-waiver.md`
- `evidence/V12-GOV-007/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan; Semih 2026-09-26 kararı):
  - tools/plan-audit/plan_audit_tool.py (V12-GOV-005) — feragat sabiti, GATES karşılaştırması, zincir istisnası.
  - tools/task-scope/task_scope_tool.py (V12-GOV-005) — feragat sabitinin kopyası ve metadata istisnası.
  - tests/Architecture/TaskScope/test_task_scope.py (V1-GOV-066) — yalnız yeni test sınıfı.
  - plan/GATES.md — işaretli feragat tablosu ve açıklaması.
  - plan/TRACEABILITY.md — `C106` kaydı.

## In scope

1. Tek kenar: `(V12-TGO-002, V12-TGO-001)`. Üç kaynak (plan_audit sabiti, task_scope kopyası, GATES tablosu)
   fail-closed çapraz doğrulanır; uyuşmazlık hata üretir.
2. Trendyol Go HTTP/webhook kodu yalnız herkese açık belgeye (`EXT:TGO-MEAL-API`) dayanan, açıkça doğrulanmamış
   taslaktır; tüketicilerin gerçek stage kanıtı isteyen maddeleri bu feragatle karşılanmış sayılmaz ve
   `V12-TGO-001`'de açık kalır. Feragat `V12-TGO-001`'i kapatmaz, aynı tüketicinin başka bağımlılığını etkilemez.

## Out of scope

- Migros Yemek (`V12-MGY-002`): belge olmadığı için feragat yok.

## Dependencies

- V12-GOV-006

## Onay

Approved by Semih — Founder/Product Owner — 2026-09-26: Trendyol Go için "Feragatle taslak olarak yaz".

## Deliverables

- Feragat sabitleri, GATES tablosu, `C106` ve testler.

## Acceptance evidence

- `plan_audit_tool.py validate` 0/0; `test_task_scope.py` yeşil.
- `task_scope_tool.py --task-id V12-GOV-007 --diff-base <InProgress commit>` exit 0.

## Handoff

- None
