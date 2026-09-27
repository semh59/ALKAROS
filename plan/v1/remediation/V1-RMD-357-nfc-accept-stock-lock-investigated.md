# V1-RMD-357 - NFC kabul akışının QR'a özgü ön-kilitlemeyi atlaması incelendi, gerçek bir kilitlenme riski değil

- Task ID: V1-RMD-357
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: investigation
- Surface state: Existing

## Source basis

- PO:2026-09-26

## Goal

Bağımsız 13 ajanlı derin denetimin (2026-09-26) düşük seviye bulgusu: `NfcOrderingStore`'un kendi kabul akışı
(`TryConsumeStockAndAcceptAsync`), `PendingOrderConfirmationStore.AcceptAsync`'in QR dalının yaptığı gibi
`OrderStockConsumptionService.LockStockRowsAsync`'i AÇIKÇA önceden çağırmıyor — teorik bir kilitlenme (deadlock)
riski olarak işaretlenmişti.

Kod incelemesiyle doğrulandı: bu iki yol YAPISAL olarak farklı, ve fark tam olarak neden birinin açık bir
ön-kilide ihtiyacı olduğunu, diğerinin olmadığını açıklıyor.

- **QR dalı** (`PendingOrderConfirmationStore.AcceptAsync`), TEK bir transaction içinde stok satırlarına İKİ
  AYRI yoldan dokunuyor: önce `ReserveQrPortionsAsync` (`ICrossChannelPortionArbiter.ReserveAsync` üzerinden,
  kanal-ötesi porsiyon rezervasyonu), sonra `ConsumeForAcceptedOrderAsync`. Bu iki yolun KENDİ kilit alma
  sıraları birbirinden bağımsız olduğundan, açık bir `LockStockRowsAsync` ön-çağrısı olmadan, iki eşzamanlı kabul
  aynı iki satıra ("ürün A" ve "ürün B" gibi) ZIT sırada dokunabilir — klasik AB-BA kilitlenme deseni
  (`OrderStockConsumptionService.cs`'in kendi V1-WTR-027 yorumu bu deseni zaten belgeliyor). Açık ön-kilit, bu
  transaction'ın dokunacağı TÜM satırları TEK, global sırada kilitleyerek bu riski kapatıyor.
- **NFC yolu** (`NfcOrderingStore.TryConsumeStockAndAcceptAsync`), aynı transaction içinde stok satırlarına
  YALNIZCA TEK bir yoldan dokunuyor: `ConsumeForAcceptedOrderAsync` → `ConsumeItemsAsync` → kendi İÇİNDE ZATEN
  `LockStockRowsAsync`'i (satır 140) global sırada çağırıyor. İkinci, bağımsız sıralı bir stok dokunuşu YOK — bu
  yüzden AB-BA döngüsünü oluşturacak iki farklı kilit sırası hiç ortaya çıkmıyor. Açık bir ön-kilit çağrısı burada
  fazladan bir adım olurdu, gerçek bir boşluğu kapatmazdı (zaten `ConsumeItemsAsync`'in kendi çağrısı aynı satır
  kümesini aynı sırada kilitliyor).

Sonuç: NFC yolunun QR'ın ön-kilit adımını atlaması bir hata değil — QR'ın ihtiyaç duyduğu ekstra adımın (ikinci,
bağımsız bir stok dokunuşuyla paylaşılan kilit sırası) NFC'de karşılığı yok.

## Owned surface

- `plan/v1/remediation/V1-RMD-357-nfc-accept-stock-lock-investigated.md`

## In scope

- Yalnızca inceleme; kod değişikliği yok.

## Out of scope

- Kod değişikliği gerekmiyor: yukarıdaki analiz, NFC yolunun ZATEN doğru ve yeterli kilitlenmeye sahip
  olduğunu gösteriyor.

## Dependencies

- None

## Acceptance evidence

- Kod incelemesi: `src/Host/Experience/NfcOrdering/NfcOrderingStore.cs:288-318`
  (`TryConsumeStockAndAcceptAsync`) tek bir stok-dokunan çağrı yapıyor (`ConsumeForAcceptedOrderAsync`).
- Kod incelemesi: `src/Host/Experience/Orders/OrderStockConsumption/OrderStockConsumptionService.cs:91-140`
  — `ConsumeForAcceptedOrderAsync` → `ConsumeItemsAsync`, ve `ConsumeItemsAsync`'in KENDİSİ `LockStockRowsAsync`'i
  (global, `OrderBy(pair => pair.StockItemId)` sıralı) satır 140'ta çağırıyor — NFC yolu bu iç çağrı sayesinde
  zaten doğru kilit sırasını alıyor.
- Kod incelemesi: `src/Host/Experience/Orders/PendingOrderConfirmation/PendingOrderConfirmationStore.cs:114-121`
  ve `:269-293` (`ReserveQrPortionsAsync`) — QR dalının AÇIK ön-kilide ihtiyacı, `ReserveQrPortionsAsync`'in
  `ICrossChannelPortionArbiter.ReserveAsync` üzerinden AYRI bir stok-dokunma yolu olmasından kaynaklanıyor; NFC
  yolunda bu ikinci yol yok.

## Handoff

- None
