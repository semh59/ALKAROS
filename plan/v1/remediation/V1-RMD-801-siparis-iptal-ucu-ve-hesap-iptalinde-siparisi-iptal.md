# V1-RMD-801 - Sipariş iptal ucu, hesap iptalinde "siparişi de iptal et" ve hesap iptali kuralının tek yere alınması

- Task ID: V1-RMD-801
- Status: Planned
- Assignee: claude-code-session_019vXeYXjE3sM6VZKpRMj1L9
- Work type: implementation
- Surface state: Existing

## Goal

Kasa incelemesinin G5 grubunun sipariş yaşam döngüsü kısmı (`7-birlesik-dogrulama.md` satır 5; `6-sinir-olay.md` B2 ve B11). Bugün hesap iptali yalnız hesabı kapatır, sipariş `Submitted` kalır ve kasa kuyruğunda hesapsız asılı durur; yemek içi (dine-in) siparişi iptal eden bir uç yoktur, stok ve günlük menü porsiyonu geri alınmaz. PO kararı (2026-11-04): hesap iptalinde "siparişi de iptal et" kutusu; hazırlanmış kalem fire, hazırlanmamış kalem stoğa döner; ayrıca izinli, gerekçeli, denetim kayıtlı ve masayı serbest bırakan bir sipariş iptal ucu.

1. Alan: `Order.CancelWhole(neden, kullanıcı, zaman)` bütün Draft/Active kalemleri iptal eder ve siparişi `Cancelled` yapar; servis edilmiş kalem varsa ve sipariş iptal edilebilir durumda değilse fırlatır.
2. Tek işlem: `OrderCancellationStore` (Host) bir işlemde şunları yapar: bill-settlement kilitleri, para kontrolü (ödeme/tahsis/akışta ödeme varsa iade önce, 409), sipariş yazımı (sürüm denetimi), kalem başına stok iadesi (Held/Sent: stoğa döner ve günlük menü porsiyonu geri verilir; Preparing/Ready: fire, stok dönmez), mutfak fişi kalemlerinin iptali, siparişin açık hesaplarının iptali, masanın serbest bırakılması (işaretçi, bağlı kasa-gönderim kaydı, birleşik masa), denetim kaydı `order.cancelled`. Denetim yazılamazsa hiçbir şey kalıcı olmaz.
3. Uç: `POST /api/v1/terminals/{terminalId}/orders/{orderId}/cancel` (`bills.cancel` izni, gerekçe 1-500 karakter zorunlu, `IdempotencyKey`, `ExpectedRowVersion`). Hesap iptal ucu `CancelBillRequestV1` içinde isteğe bağlı `CancelOrder` alır; açıksa aynı işlemde sipariş de iptal edilir (sonuçta `OrderCancelled`).
4. Hesap iptali kuralı tek yerde (B11): Billing modülünde `IBillRepository.CancelBillAsync` (işlem içi, gerekçe ve iptal eden ile) ve `CancelActiveBillsForOrderAsync` aynı SQL kalıbını paylaşır; `BillCancellationStore`, `PaymentRefunds` ve yeni sipariş iptali bunu çağırır. Masa işaretçisi ve masa serbest bırakma Tables modülünde `ITableReleasePort` arkasındadır (`OrderSettlementService`, hesap iptali, iade ve sipariş iptali bunu çağırır); Host mağazaları bu üç yerde `table_mgmt.tables` yazmaz. Para var mı kuralı (`HasMoney`) `CashierHandoffStore` ve `BillCancellationStore`daki iki kopya yerine tek sınıftadır.
5. Mimari kilit (B11): `ModuleBoundaryTests` projesine iki kural: `billing.bills` durumunu `'Cancelled'` yapan SQL yalnız Billing modülünde olur; Host/Experience altında `table_mgmt.tables` yazan dosyalar bilinen listeyle sınırlıdır ve liste yalnız küçülebilir.
6. 800'den devreden eksik (kapatılır): `OrderQuantityRule` testleri bayrak kapalı yolda ve çok büyük/0,5 katı sınır değerlerde taşmadan yargılar.

Kapsam dışı: hesapsız asılı siparişlerin yönetici temizliği, boş açık siparişlerin süpürücüsü, Paid hesap + canlı sipariş onarımı ve ilgili F-kayıtları (V1-RMD-802); arayüz (V1-RMD-803).

## Owned surface

- `evidence/V1-RMD-801/**`
- `src/Modules/Tables/TableRelease/**`
- `src/Host/Experience/Orders/OrderCancellation/**`
- `src/Host/Experience/Orders/OrderItemVoidEffects.cs`
- `src/Host/Experience/Billing/BillMoneyGuard.cs`
- `tests/Host/MigrationComposition/DualScreen/DualScreenHalfPortionTests.cs`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Modules/Orders/OrderAggregate/Order.cs (V1-CUI-011) — tüm siparişi iptal eden alan yöntemi.
  - src/Modules/Billing/BillFoundation/IBillRepository.cs (V1-RMD-002) — tek hesap iptali sözleşmesi.
  - src/Modules/Billing/BillFoundation/PostgresBillRepository.cs (V1-RMD-002) — tek SQL kalıbı.
  - src/Host/Experience/Orders/SentItemVoid/SentItemVoidStore.cs (V1-RMD-101) — kalem etkisini ortak sınıftan alır.
  - src/Host/Experience/Orders/OrderSettlementService.cs (V1-RMD-282) — masa serbest bırakma portu.
  - src/Host/Experience/Orders/CashierHandoffStore.cs (V1-RMD-279) — tek para kuralı.
  - src/Host/Experience/Billing/BillCancellation.cs (V1-RMD-506) — tek hesap iptali ve sipariş bayrağı.
  - src/Host/Experience/Billing/PaymentRefunds.cs (V1-RMD-793) — tek hesap iptali ve port.
  - src/Host/Experience/Billing/BillingSplitApplication.cs (V1-IAM-024) — sipariş iptali kayıtları.
  - src/Host/Experience/Orders/OrderManagementEndpoints.cs (V1-RMD-127) — iptal ucu ve hata eşlemesi.
  - src/Host/Experience/Orders/OrderManagementContracts.cs (V1-BIL-005) — iptal isteği ve sonucu.
  - src/Host/Composition/Errors/ApiErrorCatalog.cs (V1-RMD-489) — yeni hata kodları.
  - tests/Host/Errors/ApiErrorCatalogTests.cs (V1-RMD-786) — yeni hata satırları.
  - tests/Host/Experience/Billing/BillCancellationHttpTests.cs (V1-RMD-506) — sipariş bayrağı testleri.
  - tests/Host/Experience/Billing/OrderCancellationHttpTests.cs (V1-RMD-040) — yeni dosya: sipariş iptali uç testleri.
  - tests/Architecture/ModuleBoundaries/HostForeignWriteLockTests.cs (V1-RMD-002) — yeni dosya: B11 mimari kilidi.
  - tests/Modules/Orders/OrderAggregate/OrderDomainTests.cs (V1-RMD-467) — CancelWhole ve kural sınır testleri.
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Her kabul koşulu için önce kırmızı test (gerçek Postgres), sonra düzeltme, sonra yeşil; mutasyon turu.
2. Sipariş iptali ve hesap iptalinde sipariş bayrağı; para varsa, servis edilmiş kalem varsa, durum uygun değilse, gerekçe yoksa, yetki yoksa, denetim yazılamazsa davranışları.
3. B11 birleştirmesi ve mimari kilit.
4. İngilizce kod ve test adları; kullanıcıya dönen hata metinleri Türkçe.

## Out of scope

- Asılı siparişlerin yönetici temizliği, boş sipariş süpürücüsü, Paid hesap onarımı (V1-RMD-802); arayüz (V1-RMD-803); servis edilmiş kalemin zarar yazımı (alan kuralı: servis edilmiş kalem iptal edilemez, ödeme beklenir).

## Dependencies

- V1-RMD-800

## Acceptance evidence

1. Kırmızı sonra yeşil: `OrderDomainTests`, `OrderCancellationHttpTests`, `BillCancellationHttpTests`, `PaymentRefundHttpTests`, `RefundHalfPortionHttpTests`, VoidSent ve Comp/Void/Confirmation uç testleri, TableDraft, `ApiErrorCatalogTests`, ModuleBoundaries ve ApiConventions, gerçek exit code ile `evidence/V1-RMD-801/` altında.
2. Mutasyon: stok iadesi/fire ayrımı, para kontrolü, hesap iptali, masa serbest bırakma, denetim, izin ve mimari kural bozulunca en az bir test kırılır; kaynak `cmp` ile geri alınır.
3. Plan audit ve consistency audit 0 hata/0 uyarı; kapsam aracı exit 0.
4. qa-backend ve qa-test-kalitesi incelemeleri sırayla, bulgular giderilir, düzeltmelerden sonra doğrulama turu.
5. Semih'in elle deneyeceği senaryo: masada sipariş ver, mutfağa gönder; kasadan hesabı aç; "hesabı iptal et" ve "siparişi de iptal et" işaretle: sipariş iptal olur, kuyruktan kaybolur, masa boşalır, hazırlanmamış kalemin stoğu geri gelir.

## Handoff

- None
