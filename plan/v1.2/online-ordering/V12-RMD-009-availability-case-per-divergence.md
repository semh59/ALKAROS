# V12-RMD-009 - Stok yayını farkı vakasını her yeni farkta yeniden aç

- Task ID: V12-RMD-009
- Status: InProgress
- Assignee: Claude Opus 5.5 (session 703155c9)
- Work type: implementation
- Surface state: Existing

## Goal

V1-RMD-323 (2026-09-26) mutabakat vakası tekilliğini durumdan bağımsız yaptı: aynı anahtarlı bir vaka
çözülmüş ya da kapatılmış olsa bile yeni vaka açılmıyor. Bu kural "anahtar tek bir olaya bağlıdır" varsayımına
dayanıyor. `AvailabilityNotDelivered` kaynağının anahtarı (`online-availability:{kanal}:{ürün}`) ise aynı ürün
için tekrar tekrar oluşan bir durumu adlandırıyor. Bu yüzden bir ürün kanala bir kez ulaşmadıktan sonra, sonraki
her farkı mutabakatta hiç görünmüyor. Anahtar farkın kendisini adlandırır; V12-RMD-006'daki saat karşılaştırmalı
kapatma koruması gereksizleşir ve kaldırılır.

## Owned surface

- `evidence/V12-RMD-009/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan; Semih 2026-09-26 kararı):
  - src/Modules/Reconciliation/OnlineOrders/AvailabilityNotDeliveredSourcePair.cs (V12-REC-001) — fark başına anahtar.
  - tests/Modules/Reconciliation/OnlineOrders/ (V12-REC-001) — testler.

## In scope

1. Anahtar `online-availability:{kanal}:{ürün}:{desired_version}` olur. Aynı fark her taramada aynı vakayı
   bulur; istenen adet değişince (yeni sürüm) yeni vaka açılır.
2. Çözülen ya da kapatılan bir farkın vakası V1-RMD-323 kuralıyla bir daha açılmaz. V12-RMD-006'nın
   `resolved_at >= desired_at` kapatma koruması kaldırılır; uygulama ve veritabanı saatlerini karşılaştırmıyordu
   ve artık gerekmiyor.
3. Otomatik çözüm (`ResolveAsync`) değişmez: vakanın anahtarı taramada görünmüyorsa fark kapanmıştır.

## Out of scope

- `PostgresReconciliationRepository` (V1-RMD-323) ve diğer kaynak çiftleri; anahtarları tek bir olaya bağlı.

## Dependencies

- V12-RMD-006

## Acceptance evidence

- Reconciliation.OnlineOrders ve Host Reconciliation testleri gerçek Postgres 18 üzerinde yeşil; mutasyon kontrolü
  `evidence/V12-RMD-009/` altında.
- `task_scope_tool.py --task-id V12-RMD-009 --diff-base <InProgress commit>` exit 0.

## Handoff

- None
