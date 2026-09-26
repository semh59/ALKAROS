# V12-ONL-008 - Platformdan bağımsız gelen kutusu ve ürün eşlemesi

- Task ID: V12-ONL-008
- Status: Done
- Assignee: Claude Opus 5.5 (session 703155c9)
- Work type: implementation
- Surface state: Existing

## Goal

`yemeksepeti_webhook_inbox` ve `yemeksepeti_product_mappings` tabloları platform sütunlu ortak tablolara
dönüşür. Böylece her platform aynı tekrar ayıklama, yeniden deneme ve eşleme kurallarını kullanır.

## Owned surface

- `database/migrations/V12/V12-ONL-008/**`
- `evidence/V12-ONL-008/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan; Semih 2026-09-26 kararı):
  - src/Modules/OnlineOrdering/Yemeksepeti/WebhookInbox/ (V12-ONL-001) — ortak gelen kutusu, Yemeksepeti süzgeci.
  - src/Modules/OnlineOrdering/Yemeksepeti/OrderNormalization/ (V12-ONL-002) — ortak gelen kutusundan işleme.
  - src/Modules/OnlineOrdering/Yemeksepeti/ProductMapping/ (V12-MAP-001) — ortak eşleme tablosu.
  - src/Modules/OnlineOrdering/Yemeksepeti/Provider/ (V12-ONL-007) — teslim türü sorgusu.
  - src/Host/Experience/OnlineOrdering/ (V12-ONL-002, V12-ONL-003, V12-OUI-001) — sorgular.
  - src/Modules/Reconciliation/OnlineOrders/ (V12-REC-001) — tablo adı.
  - src/Modules/Reporting/Channels/ (V12-RPT-001) — tablo adı.
  - database/MigrationComposition/order.json — 154 numaralı migration konumu.
  - src/Host/Composition/Migrations/MigrationManifest.cs — 154 numaralı migration konumu.
  - tests/Host/MigrationComposition/Manifest/ManifestTests.cs — 154 numaralı migration konumu.
  - tests/Host/Experience/OnlineOrdering/ — testler ve fikstür bağlantıları.
  - tests/Host/Experience/Reconciliation/ — testler.
  - tests/Modules/OnlineOrdering/ — testler ve fikstür bağlantıları.
  - tests/Modules/Reconciliation/OnlineOrders/ — testler ve fikstürler.
  - tests/Modules/Reporting/Channels/ — testler ve fikstürler.

## In scope

1. Migration 154: `online_ordering.provider_inbox` (eski `yemeksepeti_webhook_inbox`) ve
   `online_ordering.provider_product_mappings` (eski `yemeksepeti_product_mappings`); ikisine de `provider`
   sütunu (küçük harfli kısa ad, varsayılan yok); mevcut satırlar `yemeksepeti`. Olay tekilliği
   `(provider, event_key)`; açık eşleme tekilliği `(provider, external_sku)` ve `(provider, product_id)`.
2. Yemeksepeti gelen kutusu, işleme, teslim türü ve ürün eşlemesi yalnız kendi platformunun satırlarını okur ve
   yazar. Bir ürünün iki platformda birer açık eşlemesi olabilir.
3. Mutabakat, kanal raporu ve operasyon kuyruğu yeni tablo adlarını kullanır; platform kırılımı V12-REC-002 ve
   V12-OUI-002'dedir.
4. Migration geri alma, başka platform satırı varsa açık hatayla durur; yoksa eski tabloları geri kurar.

## Out of scope

- Platformların kendi alım kodu; mutabakat ve ekranın platform farkındalığı.

## Dependencies

- V12-ONL-007

## Acceptance evidence

- İlgili test projeleri gerçek Postgres 18 üzerinde yeşil; mutasyon kontrolü `evidence/V12-ONL-008/` altında.
- `task_scope_tool.py --task-id V12-ONL-008 --diff-base <InProgress commit>` exit 0.
- İleri/geri migration veri kaybetmeden denenir; aynı olay anahtarı iki platformda ayrı kayıttır.

## Handoff

- None
