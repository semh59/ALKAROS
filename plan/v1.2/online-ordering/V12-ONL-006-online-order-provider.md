# V12-ONL-006 - Online siparişi hangi platformdan geldiğiyle birlikte kaydet

- Task ID: V12-ONL-006
- Status: InProgress
- Assignee: Claude Opus 5.5 (session 703155c9)
- Work type: implementation
- Surface state: Existing

## Goal

Online siparişler bugün yalnız `source = 'Online'` ve `source_external_id` ile tutuluyor. Tekillik kuralı
(migration 150) ve durum senkronu dış sipariş numarasını platformdan bağımsız arıyor. İkinci bir platform
eklendiğinde iki platformun numaraları çakışabilir. Sipariş, geldiği platformla birlikte kaydedilir.

## Owned surface

- `database/migrations/V12/V12-ONL-006/**`
- `src/Modules/OnlineOrdering/OrderLinks/**`
- `evidence/V12-ONL-006/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.
- Yol notu: `orders.orders`'a sütun eklemek sipariş deposunun her INSERT'te bağladığı alanı ve onu kullanan 21 test
  projesinin fikstürünü değiştirirdi (V1-WTR-015 dersi). Platform bağı bu yüzden OnlineOrdering modülüne ait
  ayrı bir tabloda tutulur; Order modeli değişmez. Kanal raporunun platform kırılımı V12-REC-002'ye aittir.
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan; Semih 2026-09-26 kararı):
  - src/Host/Experience/OnlineOrdering/ (V12-ONL-002, V12-ONL-003) — alım bağı yazar, senkron ve alım platformla arar.
  - database/MigrationComposition/order.json, src/Host/Composition/Migrations/MigrationManifest.cs ve
    tests/Host/MigrationComposition/Manifest/ManifestTests.cs — 153 numaralı migration konumu.
  - tests/Host/Experience/OnlineOrdering/ ve fikstür bağlantıları olan test csproj dosyaları — testler.

## In scope

1. `online_ordering.online_orders (order_id, provider, external_order_id)`: sipariş başına tek satır, tekillik
   `(provider, external_order_id)`, platform kimliği küçük harfli kısa ad biçiminde (CHECK). Mevcut online
   siparişler `yemeksepeti` olarak taşınır. `orders.orders` üzerindeki platformdan bağımsız tekil dizin
   (`uq_orders_online_source_external_id`, migration 150) kaldırılır.
2. Alım, siparişi ve platform bağını aynı transaction'da yazar; aynı platformdan ikinci sipariş veritabanında
   reddedilir, başka platformun aynı numarası kabul edilir.
3. Alım ve durum senkronu (iptal, teslim) siparişi `(platform, dış numara)` ile bulur.
4. Migration 153 ileri ve geri: geri alma, platformlar arası çakışan numara varsa açık bir hatayla durur.

## Out of scope

- Platform adaptör sözleşmesi (V12-ONL-007), ortak gelen kutusu (V12-ONL-008), rapor ve mutabakat kırılımı
  (V12-REC-002).

## Dependencies

- V12-GOV-006

## Acceptance evidence

- İlgili test projeleri gerçek Postgres 18 üzerinde yeşil; mutasyon kontrolü `evidence/V12-ONL-006/` altında.
- `task_scope_tool.py --task-id V12-ONL-006 --diff-base <InProgress commit>` exit 0.
- Migration ileri ve geri boş veritabanında denenir; aynı dış numaralı iki platform siparişi birlikte var olabilir,
  aynı platformda ikinci kayıt reddedilir.

## Handoff

- None
