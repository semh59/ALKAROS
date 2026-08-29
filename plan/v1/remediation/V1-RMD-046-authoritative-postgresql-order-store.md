# V1-RMD-046 - Authoritative PostgreSQL order store

- Task ID: V1-RMD-046
- Status: Planned
- Assignee: Unassigned
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-08-29

## Goal

`OrderManagementStore.cs` içindeki in-memory `ConcurrentDictionary` yapısını kaldırarak PostgreSQL tabanlı gerçek persistence, yetkilendirme (`Authorization`), idempotency ve host composition kaydını gerçekleştirmek.

## Owned surface

- `plan/v1/remediation/V1-RMD-046-authoritative-postgresql-order-store.md`
- `src/Host/Experience/Orders/**`
- `src/Host/Program.cs`
- `evidence/V1-RMD-046/**`

## In scope

- Draft siparişleri ve masa sipariş bağlamını PostgreSQL veritabanına ve güvenli transaction yapısına bağlamak.
- `OrderEndpoints` üzerinde terminal yetkilendirme, claim ve idempotency denetimlerini sağlamak.
- Host `Program.cs` composition root'unda servis bağımlılıklarını doğru yapılandırmak.

## Out of scope

- Kasiyer UI veya Garson PWA istemci kodlarını değiştirmek.

## Dependencies

- V1-RMD-045

## Deliverables

- Kalıcı, yetkili ve güvenli sipariş API ve store altyapısı.

## Acceptance evidence

- Sipariş oluşturma ve güncelleme işlemleri PostgreSQL üzerinde atomik olarak doğrulanır.

## Handoff

- V1-RMD-047
