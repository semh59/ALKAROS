# V1-RMD-312 - Mutabakat vakası tekilleştirmesindeki eşzamanlılık yarışını kapat

- Task ID: V1-RMD-312
- Status: Done
- Assignee: Claude Opus 5.5 (session 703155c9)
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-26

## Goal

2026-09-26 bağımsız Faz 3 denetimi (REC F3) şunu buldu: `PostgresReconciliationRepository.CreateOrDeduplicateCaseAsync`
henüz var olmayan bir satırı `SELECT ... FOR UPDATE` ile arıyor, bu da hiçbir şeyi kilitlemiyor. Aynı anahtarla
eşzamanlı iki oluşturma ikisi de ekleme yapıyor; ikincisi `uq_reconciliation_cases_active_dedup` ile `23505`
alıyor. V12-REC-001 tarayıcısı bu durumda tüm kaynağı "Kaynak okunamadı." diye raporlayıp o turun kalan
farklılıklarını atlıyor. Test yalnız vaka sayısına baktığı için bu görünmüyordu. Semih: "en küçük hata bile
kritik".

## Owned surface

- `plan/v1/remediation/V1-RMD-312-reconciliation-case-dedup-race.md`
- `evidence/V1-RMD-312/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan; Semih 2026-09-26 kararı):
  - src/Modules/Reconciliation/CaseFoundation/PostgresReconciliationRepository.cs (V1-REC-001) — anahtar başına
    transaction kilidi.
  - tests/Modules/Reconciliation/CaseFoundation/ — deterministik testler.
  - tests/Modules/Reconciliation/OnlineOrders/ (V12-REC-001) — eşzamanlı tarama testi artık kaynak hatası
    olmadığını da doğrular.

## In scope

1. Oluşturma işlemi başında `pg_advisory_xact_lock(hashtext('reconciliation-case:' || key))` alınır. Aynı anahtarla
   gelen oluşturmalar sıraya girer; sonra gelen, önce geleni bulur ve `Deduplicated` kaydeder.
2. Testler:
   - kilit tutulurken oluşturma bekler;
   - kilit bırakılınca eşzamanlı oluşturmalar tek vaka ve doğru sayıda `Deduplicated` kaydı bırakır;
   - V12-REC-001'in eşzamanlı tarama testi hiçbir kaynağın `FailureReason` üretmediğini doğrular.

## Out of scope

- V12-REC-001'in diğer bulguları (V12-RMD-006).

## Dependencies

- None

## Deliverables

- Düzeltme ve testler.

## Acceptance evidence

- İlgili test projeleri yeşil; mutasyon kontrolü `evidence/V1-RMD-312/` altında.
- `task_scope_tool.py --task-id V1-RMD-312 --diff-base <InProgress commit>` exit 0.

## Handoff

- None
