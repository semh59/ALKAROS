# V1-RMD-775 - Satın alma siparişi iptali ile mal kabul aynı anda gelince biri diğerinin durumunu ezmesin

- Task ID: V1-RMD-775
- Status: Planned
- Assignee: Unassigned
- Work type: implementation
- Surface state: Existing

## Goal

V1-RMD-740 sonrası kalan yarış. `PurchasingService.ReceiveGoodsAsync` siparişi ve durumunu kilitsiz okur; `CancelPurchaseOrderAsync` de siparişi kilitsiz okuyup tüm başlık ve satırları yazar. Kabul, iptalin kaydından sonra kilit alırsa satır miktarı değişmediği için kontrol geçer ve iptal edilmiş siparişe stok girişi yapılıp durum `PartiallyReceived`/`Completed` ile ezilir. Ters sırada iptal, kabul işlendikten sonra bayat okumasıyla durumu `Cancelled` yapar ve satırların alınan miktarını eski değerle geri yazar. Düzeltme: kilit sipariş başlığından başlar (iptal de başlığı önce yazdığı için kilit sırası aynı olur), başlığın durumu okunan durumla karşılaştırılır; iptal de aynı kilit ve doğrulamayı tek işlemde kullanır. Değişmişse işlem mevcut `PurchaseOrderStatusException` (409) ile reddedilir.

## Owned surface

- `evidence/V1-RMD-775/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Modules/Purchasing/OrdersAndReceipts/IPurchaseOrderRepository.cs (V11-PUR-001) — başlık kilidi ve durum doğrulaması.
  - src/Modules/Purchasing/OrdersAndReceipts/IPurchasingService.cs (V11-PUR-001) — iptalin tek işlemde kilitlenmesi, kabulün durumu geçirmesi.
  - tests/Modules/Purchasing/OrdersAndReceipts/OrdersAndReceiptsDatabaseTests.cs (V11-PUR-001) — yarış testleri.
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kilit yöntemi başlık satırını `FOR UPDATE` ile kilitler ve durumu beklenenle karşılaştırır, sonra satırları kilitler.
2. `ReceiveGoodsAsync` ve `CancelPurchaseOrderAsync` bu yöntemi kendi işlemleri içinde çağırır.
3. Gerçek PostgreSQL ile iki yönlü yarış testi: okuma ile yazma arasına giren iptal/kabul reddedilir ve sipariş, stok ve satır miktarı tutarlı kalır.

## Out of scope

- Başka sipariş geçişleri (Submit), tedarikçi durumu, şema/migration, Host uç noktaları.

## Dependencies

- V1-RMD-740

## Acceptance evidence

1. Yeni testler düzeltmeden önce kırmızı, sonra yeşil; Purchasing, Host ve ApiConventions testleri gerçek exit code ile `evidence/V1-RMD-775/` altında.
2. Mutasyon: başlık kilidi/durum doğrulaması kaldırılınca testler kırılır; kaynak `cmp` ile geri alınır.
3. Plan audit ve consistency audit 0 hata/0 uyarı; kapsam aracı exit 0.

## Handoff

- None
