# V12-ONL-008 - Platformdan bağımsız gelen kutusu ve ürün eşlemesi

- Task ID: V12-ONL-008
- Status: Planned
- Assignee: Unassigned
- Work type: implementation
- Surface state: Planned

## Goal

`yemeksepeti_webhook_inbox` ve `yemeksepeti_product_mappings` tabloları platform sütunlu ortak tablolara
dönüşür. Böylece her platform aynı tekrar ayıklama, yeniden deneme ve eşleme kurallarını kullanır.

## Owned surface

- `database/migrations/V12/V12-ONL-008/**`
- `evidence/V12-ONL-008/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan; Semih 2026-09-26 kararı):
  - src/Modules/OnlineOrdering/Yemeksepeti/WebhookInbox/, OrderNormalization/ ve ProductMapping/ (V12-ONL-001, V12-ONL-002, V12-MAP-001) — ortak tablolar.
  - src/Modules/OnlineOrdering/Providers/Contracts/ (V12-ONL-007) — inbox ve eşleme portları.
  - src/Host/Experience/OnlineOrdering/ ve src/Modules/Reconciliation/OnlineOrders/ (V12-REC-001) — sorgular.
  - database/MigrationComposition/order.json, src/Host/Composition/Migrations/MigrationManifest.cs ve tests/Host/MigrationComposition/Manifest/ManifestTests.cs — yeni migration konumu.
  - tests/Modules/OnlineOrdering/ tests/Modules/Reconciliation/OnlineOrders/ tests/Host/Experience/OnlineOrdering/ — testler.

## In scope

1. `online_ordering.provider_inbox (provider, event_key, ...)` ve `online_ordering.provider_product_mappings (provider, external_sku, ...)`; mevcut satırlar `yemeksepeti` olarak taşınır, eski tablolar kaldırılır.
2. Şifreli payload, tekrar ayıklama, yeniden deneme bekleme ve eşleme geçmişi kuralları platform başına korunur.
3. Migration geri alındığında Yemeksepeti satırları eski tablolara geri döner.

## Out of scope

- Platformların kendi alım kodu.

## Dependencies

- V12-ONL-007

## Acceptance evidence

- İlgili test projeleri gerçek Postgres 18 üzerinde yeşil; mutasyon kontrolü `evidence/V12-ONL-008/` altında.
- `task_scope_tool.py --task-id V12-ONL-008 --diff-base <InProgress commit>` exit 0.
- İleri/geri migration veri kaybetmeden denenir; aynı olay anahtarı iki platformda ayrı kayıttır.

## Handoff

- None
