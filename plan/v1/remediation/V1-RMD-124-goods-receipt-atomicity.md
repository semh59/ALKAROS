# V1-RMD-124 - Goods receipt atomicity (Purchasing two-phase persistence fix)

- Task ID: V1-RMD-124
- Status: Done
- Assignee: claude-session-01GGsiy81vQBnzkRKVfPsjMC
- Work type: implementation
- Surface state: Existing

## Goal

Semih onayıyla ("Evet önce denetim sonra düzeltme" — Dalga 5, database'in
Purchasing atomiklik sorunu), taze bir bağımsız denetimde doğrulanan Critical
defekti kapatır: `PurchasingService.ReceiveGoodsAsync`'in "Atomic PostgreSQL
persistence: GoodsReceipt + Order update + StockLedger movements" yorumu
yanlıştı. `_grRepo.SaveAsync(receipt, ct)` ve `_poRepo.UpdateAsync(order, ct)`
kendi ayrı bağlantılarını açıp kendi transaction'larını hemen commit
ediyordu; yalnızca sonrasındaki stok hareketi gönderimi (`_balanceRepo`/
`_movementRepo`, V11-RMD-002 ile düzeltilmişti) çağıranın `conn`/`tx`'ini
kullanıyordu. Bu, mal kabulün (goods receipt) satın alma siparişi (PO)
güncellemesiyle ve stok gönderimiyle üç ayrı, bağımsız işlemden oluştuğu
anlamına geliyordu: PO güncellemesi veya stok gönderimi herhangi bir
nedenle (bağlantı kopması, kısıt ihlali, satın alınan lokasyonun eşzamanlı
silinmesi) başarısız olursa, mal kabul kaydı zaten kalıcı olmuş oluyordu —
ama PO'nun teslim alınan miktarı hiç güncellenmiyordu ve/veya stok hiç
artmıyordu. Daha da kötüsü, `ReceiveGoodsAsync`'in kendi idempotency
kontrolü (`ReceiptNumber` benzersizliği) aynı fiş numarasıyla bir tekrar
denemeyi "zaten var" diye reddediyordu — yarı uygulanmış bu durumdan
kurtulmanın hiçbir yolu yoktu.

## Owned surface

- `plan/v1/remediation/V1-RMD-124-goods-receipt-atomicity.md` (yeni)
- Sınırlı ek — aşağıdaki yollar ilgili görevlerin sahipliğinde kalır
  (yollar geri-tik olmadan yazıldı ki denetleyici bunları sahiplik iddiası
  olarak parse etmesin):
  - src/Modules/Purchasing/OrdersAndReceipts/IGoodsReceiptRepository.cs,
    IPurchaseOrderRepository.cs, IPurchasingService.cs (V11-PUR-001
    sahipliğinde) — `IGoodsReceiptRepository.SaveAsync` ve
    `IPurchaseOrderRepository.UpdateAsync`'e `IStockBalanceRepository`/
    `IStockMovementRepository`'nin zaten sahip olduğu desende bir
    connection/transaction-alan overload; `ReceiveGoodsAsync` artık
    fiş kaydını, PO güncellemesini ve stok gönderimini TEK bir paylaşılan
    transaction'da yürütüyor; yanıltıcı "Atomic" yorumu gerçeği yansıtacak
    şekilde güncellendi.
  - tests/Modules/Purchasing/OrdersAndReceipts/OrdersAndReceiptsDatabaseTests.cs
    (ilgili görev sahipliğinde) — bu görevin regresyon testi.

## In scope

1. **`IGoodsReceiptRepository.SaveAsync(receipt, connection, transaction, ct)`**
   ve **`IPurchaseOrderRepository.UpdateAsync(order, connection, transaction, ct)`**:
   `SaveAsync`/`UpdateAsync`'in var olan çağıranı olmayan (single-arg)
   overload'ları artık bu yenilerine delege ediyor; davranış (idempotency
   kontrolü, `rows == 0` fırlatması) değişmedi.
2. **`ReceiveGoodsAsync`**: fiş kaydı ve PO güncellemesi artık
   `_dataSource.OpenConnectionAsync`+`BeginTransactionAsync`'ten gelen AYNI
   `conn`/`tx`'i kullanıyor — stok gönderiminin zaten kullandığı gibi. Üç
   adımdan biri başarısız olursa hiçbiri kalıcı olmuyor; fiş numarası
   idempotency kontrolü artık gerçekten "hiç denenmemiş" ile "yarı
   uygulanmış" durumları karıştırmıyor, çünkü ikincisi artık var olamıyor.

## Out of scope

- `CreatePurchaseOrderAsync`/`SubmitPurchaseOrderAsync`/`CancelPurchaseOrderAsync`'in
  kendi `SaveAsync`/`UpdateAsync` çağrıları — bunlar zaten tek başına
  atomik tek-yazma işlemleri, başka bir kaynakla paylaşılan bir
  transaction'a ihtiyaçları yok.
- Diğer denetim adayları — bu dalganın hedefi yalnızca bu tek atomiklik
  bulgusuydu.

## Dependencies

- V1-RMD-123

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Debug`: 0 uyarı / 0 hata.
- `docker compose -f compose.yaml -f compose.test.yaml up --build test`:
  `docker inspect alkaros-test-1` ile gerçek container exit code
  doğrulandı; `ALKAROS.Purchasing.OrdersAndReceipts.Tests` (yeni
  `AFailureDuringStockPostingRollsBackTheReceiptAndOrderUpdateToo` dahil,
  var olan `ReceiveGoodsExactDeliveryPostsStockMovementAndCompletesOrder`/
  `PartialReceiptFollowedByRemainderCompletesOrder`/vb. davranışı
  değişmeden) yeşil.
- Yeni test, hedef lokasyonu siparişten sonra silip (bu iki tablonun
  şemasında `inventory.stock_locations`'a kasıtlı olarak hiç cross-schema
  FK olmadığından fiş/PO kaydı etkilenmiyor, yalnız gerçek FK taşıyan stok
  gönderimi başarısız oluyor) gerçek bir `PostgresException` tetikliyor ve
  ardından fişin hiç kalıcı olmadığını, PO'nun teslim alınan miktarının
  hâlâ 0 olduğunu doğruluyor.
- Revert-and-confirm: `ReceiveGoodsAsync`'teki `SaveAsync`/`UpdateAsync`
  çağrıları geçici olarak eski single-arg overload'lara geri alınıp yeni
  test çalıştırıldığında **gerçekten ve tam olarak beklenen şekilde**
  başarısız oldu — `_grRepo.GetByReceiptNumberAsync(rcptNum)` fişi kalıcı
  bulundu (stok gönderimi başarısız olmasına rağmen). Düzeltme geri
  getirilip tam süit yeniden `docker inspect` ile doğrulanan exit code 0,
  80 test projesi, sıfır başarısız.
- `python tools/consistency-audit/consistency_audit.py`: 13 ihlal, hepsi bu
  görevden önce de vardı, dokunulmayan dosyalarda.
- `python tools/plan-audit/plan_audit_tool.py validate`: 0 hata.

## Handoff

- V1-GOV-123
