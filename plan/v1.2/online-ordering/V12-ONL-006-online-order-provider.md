# V12-ONL-006 - Online siparişi hangi platformdan geldiğiyle birlikte kaydet

- Task ID: V12-ONL-006
- Status: Planned
- Assignee: Unassigned
- Work type: implementation
- Surface state: Planned

## Goal

Online siparişler bugün yalnız `source = 'Online'` ve `source_external_id` ile tutuluyor. Tekillik kuralı
(migration 150) ve durum senkronu dış sipariş numarasını platformdan bağımsız arıyor. İkinci bir platform
eklendiğinde iki platformun numaraları çakışabilir. Sipariş, geldiği platformla birlikte kaydedilir.

## Owned surface

- `database/migrations/V12/V12-ONL-006/**`
- `evidence/V12-ONL-006/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan; Semih 2026-09-26 kararı):
  - src/Modules/Orders/OrderAggregate/ (V1-ORD-001) — online platform alanı.
  - src/Host/Experience/OnlineOrdering/ (V12-ONL-002, V12-ONL-003, V12-OUI-001) — alım, senkron ve kuyruk sorguları.
  - src/Modules/Reporting/Channels/ (V12-RPT-001) — platform kırılımı.
  - database/MigrationComposition/order.json, src/Host/Composition/Migrations/MigrationManifest.cs ve tests/Host/MigrationComposition/Manifest/ManifestTests.cs — yeni migration konumu.
  - tests/Modules/Orders/ tests/Host/Experience/OnlineOrdering/ tests/Modules/Reporting/Channels/ — testler.

## In scope

1. `orders.orders.online_provider` (yalnız `source = 'Online'` için dolu, CHECK ile); mevcut online siparişler `yemeksepeti` olarak taşınır. Tekillik `(online_provider, source_external_id)` olur.
2. Durum senkronu, iptal ve teslim sorguları platformla birlikte arar.
3. Kanal raporu online satırlarını platforma göre ayırır.

## Out of scope

- Platform adaptör sözleşmesi (V12-ONL-007).

## Dependencies

- V12-GOV-006

## Acceptance evidence

- İlgili test projeleri gerçek Postgres 18 üzerinde yeşil; mutasyon kontrolü `evidence/V12-ONL-006/` altında.
- `task_scope_tool.py --task-id V12-ONL-006 --diff-base <InProgress commit>` exit 0.
- Migration ileri ve geri boş veritabanında denenir; aynı dış numaralı iki platform siparişi birlikte var olabilir, aynı platformda ikinci kayıt reddedilir.

## Handoff

- None
