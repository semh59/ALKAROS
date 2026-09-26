# V12-MGY-002 - Migros Yemek sipariş adaptörü

- Task ID: V12-MGY-002
- Status: Planned
- Assignee: Unassigned
- Work type: integration
- Surface state: Planned

## Goal

Migros Yemek siparişlerini ortak adaptör sözleşmesiyle (V12-ONL-007) almak, durumlarını bildirmek ve menüyü
eşlemek. Kapsam, V12-MGY-001 ile alınacak resmî belgeye göre kesinleşir; belge olmadan hiçbir alan, durum veya
davranış varsayılmaz.

## Owned surface

- `src/Modules/OnlineOrdering/Providers/MigrosYemek/**`
- `tests/Modules/OnlineOrdering/Providers/MigrosYemek/**`
- `evidence/V12-MGY-002/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan; Semih 2026-09-26 kararı):
  - src/Modules/OnlineOrdering/OnlineOrderingModule.cs (V12-MAP-001) — kayıt.

## In scope

1. Sipariş alma (belgedeki yola göre webhook ya da V12-ONL-009 çekmesi), durum bildirimi, iptal ve menü eşlemesi.

## Out of scope

- Belgede olmayan her davranış.

## Dependencies

- V12-MGY-001
- V12-ONL-009

## Acceptance evidence

- İlgili test projeleri gerçek Postgres 18 üzerinde yeşil; mutasyon kontrolü `evidence/V12-MGY-002/` altında.
- `task_scope_tool.py --task-id V12-MGY-002 --diff-base <InProgress commit>` exit 0.

## Handoff

- None
