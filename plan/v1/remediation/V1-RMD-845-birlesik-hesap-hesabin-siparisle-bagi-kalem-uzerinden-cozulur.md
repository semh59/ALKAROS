# V1-RMD-845 - Birleşik hesap: hesabın siparişle bağı kalem üzerinden çözülür

- Task ID: V1-RMD-845
- Status: Planned
- Assignee: claude-code-session_01FMN5WTVUmS2xYXu1gL4Tsz
- Work type: implementation
- Surface state: Existing

## Goal

Kasa incelemesi bulgusu F0046 (PO:2026-11-06: özellik yazılacak, silinmeyecek): birden çok siparişin kalemlerini tek hesapta toplayan akış. Alan modeli bunu zaten tarif ediyor (`BillSourceOperations.CreateMergedBill`, `orderId: null`), ama üretimde hiçbir yol çağırmıyor ve hesabın siparişle bağına bakan çok sayıda tüketici `billing.bills.order_id` sütununa dayanıyor. Birleşik hesap `order_id = NULL` taşıdığında bu tüketiciler hesabı göremiyor: ödenmiş birleşik hesap hiçbir siparişi kapatmıyor, "askıda sipariş" temizliği kalemleri açık hesapta olan siparişi iptal edebiliyor, ödenmiş kalem iptal edilebiliyor, kasaya gönderilmiş birleşik hesap masada kalıyor.

Bu görev uç nokta eklemeden önce tek ilişkiyi kurar: bir hesap bir siparişe `b.order_id = o.order_id` VEYA kalem yoluyla (`billing.bill_items.order_item_id` -> `orders.order_items.order_id`) bağlıdır. Billing deposu bu ilişkiyi tek yerde sunar (`GetByOrderIdAsync` kalem yolunu da kapsar, yeni `GetOrderIdsAsync(billId)`), ham SQL tüketicileri aynı ilişkiyi kullanır, ödeme sonrası kapatma hesabın bütün kaynak siparişlerini tamamlar. Henüz birleşik hesap açan bir uç yoktur; testler birleşik hesabı alan modeliyle (`CreateMergedBill`) tohumlar.

## Owned surface

- `evidence/V1-RMD-845/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Modules/Billing/BillFoundation/IBillRepository.cs — kalem yolu ve kaynak sipariş sorgusu.
  - src/Modules/Billing/BillFoundation/PostgresBillRepository.cs — kalem yolu, kaynak sipariş sorgusu, siparişe bağlı iptalde kalem serbest bırakma.
  - src/Host/Experience/Orders/OrderSettlementService.cs — her kaynak siparişi tamamlama.
  - src/Host/Experience/Billing/BillClosureSweeper.cs — onarım sorgusu.
  - src/Host/Experience/SecurityAdministration/OrderBacklogAdministration.cs — bekleyen sipariş sayımı ve toplu kapatma.
  - src/Host/Experience/Billing/BillCancellation.cs — birleşik hesapta `cancelOrder` davranışı.
  - src/Host/Experience/Billing/BillMoneyGuard.cs — siparişin hesapları ve para denetimi.
  - src/Host/Experience/Orders/OrderCancellation/OrderCancellationStore.cs — sipariş iptalinde birleşik hesap.
  - src/Modules/Tables/TableRelease/PostgresTableReleasePort.cs — masa işaretçisi ve masayı bırakma.
  - src/Modules/Tables/TableTransfer/PostgresTableTransferRepository.cs — kasaya gönderilmiş hesap ayrımı.
  - src/Modules/Tables/TableMerge/PostgresTableMergeRepository.cs — kasaya gönderilmiş hesap ayrımı.
  - src/Modules/Tables/CurrentPointers/PostgresTablePointerProjector.cs — kasaya gönderilmiş hesap ayrımı.
  - src/Host/Experience/Orders/CashierHandoffStore.cs — kasaya gönderme/geri alma ve kuyruk.
  - src/Host/Experience/Orders/OrderCleanup/OrderCleanupStore.cs — askıda sipariş ölçütü.
  - src/Host/Experience/Billing/BillingSplitStore.cs — kalem kısıtı ihlalinin yarış kurtarması.
  - tests/Modules/Billing/BillFoundation/ — ilişki ve iptal testleri.
  - tests/Host/Experience/Billing/ — tüketici testleri.
  - tests/Host/Experience/Orders/ — kalem koruması ve temizlik testleri.
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Önce kırmızı testler (gerçek Postgres): birleşik hesap ödenince kaynak siparişlerin hepsi tamamlanır ve masa bırakılır; kalemleri açık birleşik hesapta olan sipariş askıda-temizlikle iptal edilmez; ödenmiş birleşik hesaptaki kalem iptal/ikram edilemez; sipariş iptali birleşik hesabı ve kalem kilitlerini serbest bırakır; kasaya gönderilmiş birleşik hesap masadan ayrılır; `from-order` birleşik hesabı olan siparişe ikinci hesap açmaz.
2. Düzeltme, yeşil, mutasyon turu (`cp` + `cmp` ile geri alınır).
3. `[C]` sınıfı rapor ve gösterim tüketicileri (kanal raporu, müşteri ekranı ödeme ilerlemesi, kasa kuyruğu satırı) için bu görevde yapılan veya sonraya bırakılan her karar `evidence/V1-RMD-845/` altında gerekçesiyle listelenir.

## Out of scope

- Birleşik hesabı açan uç ve kasa ekranı (V1-RMD-846, V1-RMD-847); siparişin kalemlerini birden çok hesaba bölen akış (`CreateSplitBills`).

## Dependencies

- V1-RMD-844

## Acceptance evidence

1. Billing, BillFoundation, Orders ve Tables test projeleri tam yeşil; kırmızı/yeşil çıktı `evidence/V1-RMD-845/` altında.
2. Mutasyon: her tüketicide kalem yolunu kaldırmak ilgili testi düşürür.
3. Plan audit ve consistency audit 0 hata/0 uyarı; kapsam aracı exit 0.
4. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
