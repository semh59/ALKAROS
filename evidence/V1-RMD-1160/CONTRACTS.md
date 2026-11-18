# V1-RMD-1160 sozlesme ve yetki dogrulamasi (kodla)

Canli istemci: Dockerfile (satir 54-73) tek statik agaci kurar; PosTerminal React kokte, vanilla Cashier `/cashier` altinda sunulur. PosTerminal yalniz `/cashier/payments/*` sayfalarina baglanir; vanilla hizli satis sayfasina (`/cashier/index.html`) PosTerminal'den baglanti yoktur (grep `src/Clients`). Yani kasanin canli satis ekrani PosTerminal `Cashier.tsx`; vanilla sayfa hala sunuluyor ama baglantisiz.

## (a) Kalem notu: sozlesme EKSIK, yapilmadi
- PosTerminal kalem ekler: `POST /api/v1/terminals/{t}/orders/{o}/items`, govde `AddOrderItemRequest(ProductId, Quantity, ExpectedRevision, Modifiers)` (`src/Host/DualScreen/DualScreenContracts.cs:114`). Not alani yok.
- `DualScreenStore.AddItemAsync` (`DualScreenStore.Orders.cs:252-340`) `new OrderItem(...)` cagrisinda `notes:` vermez; ayni kalemin tekrarinda birlesme kosulu `string.IsNullOrWhiteSpace(i.Notes)` oldugu icin notlu kalem ileride ayri satir olmali.
- `ChangeOrderItemQuantityRequest(Quantity, ExpectedRevision)` de not tasimaz; sonradan not eklenemez. Siparis notu icin de aynisi: `StartOrderRequest(TableId, ExpectedTableRowVersion)` ve `SubmitOrderRequest(OperationId, ExpectedRevision)` not tasimaz.
- `SpecialInstructions` (`OrderItemDraftDto`) ve `OrderNote` (`CreateTableDraftRequest`) yalniz `POST .../orders/table-draft` yolunda vardir; vanilla kasa (`cashier-app.js:791`) o yolu kullanir. PosTerminal akisi (terminal taslagi, musteri ekrani, `/items`) bu yola tasinamaz: tum sepet akisinin yeniden yazimi olur.
- Mutfak tarafi hazir: `TableDraftService.cs:171` (notes: SpecialInstructions) -> `KitchenTicket.cs:375` -> `EscPosTicketFormatter.cs:87-89` fise `NOT: ...` basar. Eksik olan yalniz terminal `/items` uc noktasi.
- Eksigin tam yeri: Host'ta `AddOrderItemRequest`'e not alani + `AddItemAsync`'te `OrderItem(notes:)` + birlesme kurali; ardindan istemci. Ayri Host gorevi gerekir.

## (b) Beklet: sozlesme YOK, yapilmadi
- Sunucuda park/beklet taslak sozlesmesi yok (grep src/Host, src/Modules, database: yalniz kurs "beklet" yorumu, `105-order-items-course.up.sql`, ilgisiz).
- Terminal basina tek aktif taslak kurali: `DualScreenStore.Orders.cs:73` (`TerminalHoldsDraftMessage`) ikinci taslagi reddeder; park icin sunucu tarafinda ikinci taslak saklanamaz.
- Vanilla "Beklet" yalniz `localStorage` (`cashier-app.js:56-80, 893-923`): tarayici/cihaz kaybinda fis kaybolur. Istemci bellegine sahte park veri kaybi riskidir; yapilmadi.
- Eksik: Host'ta kalici park/askiya alma sozlesmesi (depo + uc nokta + yetki) ve terminal tek-taslak kuralinin buna gore degismesi.

## (c) Gonderilmis kalem iptali: sozlesme TAM, YAPILDI
- `POST /api/v1/terminals/{t}/orders/{o}/items/{i}/void-sent`, `VoidSentItemRequestV1(IdempotencyKey, ExpectedRowVersion, ReasonCode, Notes?)` -> `VoidSentItemResultV1(Status Applied|Pending, ..., StockRestored)` (`OrderManagementEndpoints.cs:807-914`).
- Gerekce kodlari `VoidReasonCatalog` (`ReasonCatalogs.cs:7-26`): CustomerChange, OperatorError, ProductUnavailable, DuplicateEntry.
- Yetki: kasa oturumu zorunlu; `bills.void` izni kasiyer rolunde dogrudan YOK (`ApplicationPermissions.cs:147-150`, `SupervisorEscalations`). Kasiyer isteyince yetki talebi acilir: 202 `Pending` (yonetici onayi), onaylaninca ayni anahtarla yeniden gonderim uygulanir; reddedilirse 403 `GRANT_DENIED`. Izne sahip rol (supervisor/yonetici) dogrudan `Applied`. Yonetici onay ekrani PosTerminal'de var (`features/authorization-decisions`).
- Uygunluk: `GET .../orders/table/{tableId}` `OrderDtoAssembler.MapToDto` ile kalem basina `CanVoidSent` verir (`OrderDtoAssembler.cs:254-256`: Aktif, mutfaga gitmis, Served/Cancelled degil). Istemci bu bayragi kullanir, kendi turetmez.
- Servis edilmis kalem: `SentItemVoidStore.LoadVoidableAsync` Served kalemi HERKES icin reddeder (`ItemAlreadyServedException`). Semih kurali (servis edilmis kalem/hesap yalniz yetkili yonetici) bu uc noktada degil, hesap iptali yolunda (`PaymentsRefundApprove`) uygulanir; kasada mevcut "Iptal / iade" ve `servedBillNote` o yolu anlatir. Bu gorev servis edilmis kalem icin dugme cikarmaz.
- DUZELTME (qa-2, `Order.cs:458`, `SubmitOrderHandler.cs:200`): gonderim aninda kalem ayardan bagimsiz `Sent` olur; `kitchen.live_sync_enabled` yalniz sonraki gecisleri (Preparing/Ready/Served) yansitir. Ayar kapaliyken her gonderilmis kalem `CanVoidSent=true` alir; yani dugme servis edilip yenmis kalemde de gorunur (yorum `OrderManagementEndpoints.cs:800-806` eski). Kasiyer icin yonetici onayi engeli kalir; supervisor/yonetici onaysiz uygular ve stok geri alinir.
