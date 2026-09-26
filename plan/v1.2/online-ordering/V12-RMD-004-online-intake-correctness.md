# V12-RMD-004 - Online sipariş alımının doğruluk bulgularını kapat

- Task ID: V12-RMD-004
- Status: Done
- Assignee: Claude Opus 5.5 (session 703155c9)
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-26

## Goal

2026-09-26 bağımsız Faz 3 denetiminin sipariş alımı (V12-ONL-002) bulgularını kapatmak (Semih: "en küçük hata
bile kritik"):

- Sağlayıcıdan iptal istenmiş bir sipariş, yeni zaman damgalı ikinci bir RECEIVED ile yine de oluşabiliyor.
- `items[].instructions` okunmuyor; ürün başına notlar (ör. alerji) sessizce kayboluyor. `items[].status` ve
  `replaced_id` yok sayılıyor; değiştirilmiş veya kaldırılmış kalem de satır oluyor.
- Sağlayıcının `payment.sub_total` değeri yerel toplamla karşılaştırılmıyor; fark hiçbir kayıt bırakmıyor.
- Tanınmayan bir `transport_type` ile kabul edilen sipariş hiçbir zaman kuryeye teslim edilemiyor.
- Deneme hakkı bekleme olmadan yaklaşık 25 saniyede tükeniyor. Kapanıştan kaynaklanmayan bir zaman aşımı
  (`OperationCanceledException`) hiç sayılmıyor.
- "Sağlayıcı siparişi başına tek sipariş" yalnız advisory kilide dayanıyor; veritabanında benzersizlik yok.

## Owned surface

- `plan/v1.2/online-ordering/V12-RMD-004-online-intake-correctness.md`
- `evidence/V12-RMD-004/**`
- `database/migrations/V12/V12-RMD-004/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan; Semih 2026-09-26 kararı):
  - src/Modules/OnlineOrdering/Yemeksepeti/OrderNormalization/ (V12-ONL-002) — normalizasyon, işleme deposu.
  - src/Host/Experience/OnlineOrdering/YemeksepetiOrderIntakeService.cs (V12-ONL-002) — alım akışı.
  - src/Modules/Reconciliation/OnlineOrders/ (V12-REC-001) — sağlayıcı toplam farkı kaynak çifti.
  - src/Clients/PosTerminal/src/features/online-operations/ (V12-OUI-001) — yeni ret nedenlerinin Türkçe
    etiketleri.
  - database/MigrationComposition/order.json, src/Host/Composition/Migrations/MigrationManifest.cs ve
    tests/Host/MigrationComposition/Manifest/ManifestTests.cs — 150 numaralı migration konumu.
  - tests/Modules/OnlineOrdering/Yemeksepeti/OrderNormalization/ tests/Host/Experience/OnlineOrdering/
    tests/Modules/Reconciliation/OnlineOrders/ ve tests/Host/Experience/Reconciliation/ — testler.

## In scope

1. İkinci RECEIVED koruması. Aynı sağlayıcı siparişi için daha önce `providerCancellationRequested = true` ile
   reddedilmiş bir olay varsa yeni RECEIVED sipariş oluşturmaz; `SkippedCancellationRequested` olarak kapanır.
2. Kalemler:
   - `instructions` temizlenip sınırlanır ve sipariş kaleminin notu olur (mutfak talimatı, PO:2026-09-01).
   - `status`, belgede yalnız örneği olan `IN_CART` veya yok ise normal kalemdir.
   - Başka bir durum ya da dolu `replaced_id` → `UnsupportedItemStatus` ile ret, otomatik iptal yok (insana kalır).
   - Bu davranış doğrulanmamış taslaktır.
3. `payment.sub_total` varsa satır toplamlarıyla karşılaştırılır. Fark siparişi durdurmaz: işlem kaydına
   `totalsMatch=false` ve iki toplam yazılır, V12-REC-001'e `ProviderTotalMismatch` kaynak çifti eklenir.
4. `transport_type` yalnız `LOGISTICS_DELIVERY` ve `VENDOR_DELIVERY` olabilir; başka değer
   `UnsupportedTransportType` ile ret, otomatik iptal yok.
5. Deneme:
   - üstel bekleme (`next_attempt_at`, migration 150; 10 sn'den başlar, 10 dk ile sınırlı), `MaxAttempts = 8`;
   - kapanıştan kaynaklanmayan `OperationCanceledException` de bir deneme sayılır.
6. `orders.orders (source_external_id) WHERE source = 'Online'` benzersiz indeksi (migration 150).
7. PosTerminal'de yeni ret nedenlerinin Türkçe etiketleri.

## Out of scope

- Webhook ucunda kimlik doğrulama sırası ve müşteri notunun şifreli tutulması (V12-RMD-007).
- Giden akış bulguları (V12-RMD-005).

## Dependencies

- V1-RMD-310

## Deliverables

- Yukarıdaki kod, migration 150 (up/down) ve testler.

## Acceptance evidence

- İlgili test projeleri yeşil; migration 150 geri alınıp yeniden uygulanır; mutasyon kontrolü
  `evidence/V12-RMD-004/` altında.
- `task_scope_tool.py --task-id V12-RMD-004 --diff-base <InProgress commit>` exit 0.

## Handoff

- None
