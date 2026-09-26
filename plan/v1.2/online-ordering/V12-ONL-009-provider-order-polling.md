# V12-ONL-009 - Webhook'u olmayan ya da kapanan platformlar için sipariş çekme altyapısı

- Task ID: V12-ONL-009
- Status: Done
- Assignee: Claude Opus 5.5 (session 703155c9)
- Work type: implementation
- Surface state: Planned

## Goal

Trendyol Go, kapalı kalan webhook alıcısının entegrasyonunu kapatabiliyor. Migros Yemek POS firmaları
siparişleri periyodik çekiyor. Platform adaptörünün sağladığı "sipariş listesi" çağrısıyla siparişleri çekip
aynı gelen kutusuna yazan zamanlanmış servis kurulur.

## Owned surface

- `src/Modules/OnlineOrdering/Polling/**`
- `tests/Modules/OnlineOrdering/Polling/**`
- `src/Host/Experience/OnlineOrdering/OnlineOrderPollingHostedService.cs`
- `database/migrations/V12/V12-ONL-009/**`
- `evidence/V12-ONL-009/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan; Semih 2026-09-26 kararı):
  - src/Modules/OnlineOrdering/Providers/Contracts/ (V12-ONL-007) — isteğe bağlı çekme portu.
  - src/Modules/OnlineOrdering/OnlineOrderingModule.cs (V12-MAP-001) — kayıt.
  - src/Host/DualScreen/DualScreenApplication.cs — zamanlanmış servisin kaydı (V12-GOV-008).
  - database/MigrationComposition/order.json — migration konumu (V12-GOV-008).
  - src/Host/Composition/Migrations/MigrationManifest.cs — migration konumu (V12-GOV-008).
  - tests/Host/MigrationComposition/Manifest/ManifestTests.cs — migration konumu (V12-GOV-008).
  - tests/Host/Experience/OnlineOrdering/ — testler ve fikstür bağlantıları (V12-GOV-008).
  - src/Modules/Reconciliation/OnlineOrders/ (V12-REC-001) — çekme hatası kaynak çifti (In scope 3).
  - tests/Modules/Reconciliation/OnlineOrders/ — kaynak çifti testleri ve fikstür bağlantısı.
  - tests/Host/Experience/Reconciliation/ — kayıtlı kaynak çifti sayısı.

## In scope

1. Platform başına çekme aralığı ve son okunan konum (imleç) kalıcı tutulur; çekilen sipariş webhook'la gelmiş olsa bile gelen kutusu tekrarını ayıklar.
2. Platformun hız sınırına uyulur; 429 yanıtı çekmeyi geciktirir, sipariş kaybettirmez.
3. Çekme hatası mutabakatta görünür (V12-REC-002).

## Out of scope

- Platforma özel çekme uç noktaları (adaptör görevlerinde).

## Dependencies

- V12-ONL-008
- V12-ONL-010

## Acceptance evidence

- İlgili test projeleri gerçek Postgres 18 üzerinde yeşil; mutasyon kontrolü `evidence/V12-ONL-009/` altında.
- `task_scope_tool.py --task-id V12-ONL-009 --diff-base <InProgress commit>` exit 0.
- Aynı sipariş hem webhook hem çekmeyle geldiğinde tek sipariş oluşur.

## Handoff

- None
