# V1-ORD-005 - Pre-send item void endpoint

- Task ID: V1-ORD-005
- Status: Blocked
- Assignee: Unassigned
- Work type: implementation
- Surface state: Existing

## Goal

`ItemExceptionHandler.VoidItemAsync` (`Orders/ItemExceptions/**`) zaten tam
çalışır durumda — henüz mutfağa gönderilmemiş (`KitchenState = NotSent`) bir
kalemi iptal eder, sebep kataloğu ve denetim kaydı ile. Ama hiçbir HTTP
endpoint'i onu çağırmıyor: bu servis bugün ölü koddur. Bu görev, mevcut
domain mantığına dokunmadan, onu gerçek bir uç noktaya bağlar. Bu, `V1-SET-002`
/ `V1-KIT-005`'e bağımlı değildir — anahtar kapalıyken de (bugünkü tek
gerçekleşen durum) çalışır.

## Owned surface

- `plan/v1/orders/V1-ORD-005-pre-send-void-endpoint.md`
- `evidence/V1-ORD-005/**`
- Bu görev, başka bir task'in owned surface alanını başka şekilde
  değiştiremez.

## In scope

- `src/Host/Experience/Orders/OrderManagementEndpoints.cs`'e
  `POST /api/v1/terminals/{terminalId}/orders/{orderId}/items/{itemId}/void`
  eklemek: `orders.create` izniyle (model §2: "unsent items need only
  orders.create"), `ItemExceptionHandler.VoidItemAsync`'i çağırır.
- `VoidReasonCatalog`'daki dört sebepten birini isteyen bir sözleşme.
- `IsManagerAuthorized` alanının kaldırılması veya `orders.create` iznine
  eşlenmesi (mevcut alan `ItemExceptionHandler`'ın kendi eski varsayımı;
  granüler izin modeliyle tutarlı hale getirilir).

## Out of scope

- Gönderildi-ama-servis-edilmedi void (`V1-IAM-027`).
- Comp (`V1-BIL-005`).

## Dependencies

- V1-IAM-024

## Blocker

- `src/Host/Experience/Orders/OrderManagementEndpoints.cs` (sahip:
  `V1-IAM-024`) ve `src/Modules/Orders/ItemExceptions/**` (sahip: `V1-ORD-004`)
  için custody devri veya sınırlı-ek notu implementasyon başlarken eklenir.
  Ancak bu eklenip `validate` temiz kaldığında görev `Planned` yapılabilir.

## Acceptance evidence

- (implementasyon tamamlandıktan sonra doldurulur.)

## Handoff

- V1-IAM-027
