# V12-REC-002 - Mutabakat vakalarını ve kanal raporunu platform bazında çalıştır

- Task ID: V12-REC-002
- Status: Planned
- Assignee: Unassigned
- Work type: implementation
- Surface state: Planned

## Goal

Online mutabakat kaynak çiftleri ve kanal raporu yalnız Yemeksepeti tablolarını okuyor. Her vaka ve rapor satırı
hangi platforma ait olduğunu taşır; sonraki bir platform aynı kurallarla görünür olur.

## Owned surface

- `evidence/V12-REC-002/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan; Semih 2026-09-26 kararı):
  - src/Modules/Reconciliation/OnlineOrders/ (V12-REC-001) — platform alanı ve süzgeci.
  - src/Modules/Reporting/Channels/ (V12-RPT-001) — platform kırılımı.
  - src/Host/Experience/Reconciliation/ (V12-REC-001) — uç nokta süzgeci.
  - tests/Modules/Reconciliation/OnlineOrders/ tests/Modules/Reporting/Channels/ tests/Host/Experience/Reconciliation/ — testler.

## In scope

1. Her kaynak çifti tüm platformları tarar; tekillik anahtarı platformu içerir.
2. Vaka ayrıntısı ve rapor satırı platform kimliğini taşır; uç noktalar platforma göre süzülebilir.
3. Çekme servisi hataları (V12-ONL-009) kendi fark türüyle görünür.

## Out of scope

- Platforma özel yeni fark türleri (adaptör görevlerinde).

## Dependencies

- V12-ONL-008

## Acceptance evidence

- İlgili test projeleri gerçek Postgres 18 üzerinde yeşil; mutasyon kontrolü `evidence/V12-REC-002/` altında.
- `task_scope_tool.py --task-id V12-REC-002 --diff-base <InProgress commit>` exit 0.

## Handoff

- None
