# V1-RMD-216 - `/orders/table/{tableId}` ve `/orders/awaiting-payment` yetki kontrolsüzdü

- Task ID: V1-RMD-216
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

Bağımsız çok-ajanlı denetimin (2026-09-16) bulduğu iki bulgu, aynı
kalıbı paylaştığı için tek görevde birleştirildi: `GET
/orders/table/{tableId}` (HIGH) ve `GET /orders/awaiting-payment`
(MEDIUM) yalnızca `RequireCashierSessionAsync` (canlı bir terminal
oturumu) çağırıyordu, hiçbir `ApplicationPermissions.*` kontrolü
yoktu — tam olarak kardeş endpoint `GET /{orderId}`'nin V1-RMD-160 ile
düzeltildiği aynı boşluk, bu ikisine hiç uygulanmamış. `orders.create`
taşımayan (yalnız `orders.send` gibi) bir oturum, herhangi bir masanın
tam sipariş/kalem/tutar bilgisini ya da kasaya gönderilmiş tüm
hesapların ödeme kuyruğunu okuyabiliyordu.

## Owned surface

- src/Host/Experience/Orders/OrderManagementEndpoints.cs (ilgili
  modülün sahipliğinde)
- tests/Host/Experience/Orders/TableDraft/OrderManagementTableDraftHttpTests.cs
  (aynı modül)

## In scope

1. Her iki endpoint de artık `RequireCashierPermissionAsync(...,
   ApplicationPermissions.OrdersCreate, ...)` kullanıyor — `/pending`
   ve (V1-RMD-160 ile düzeltilmiş) `GET /{orderId}` ile aynı yetki
   seviyesi, aynı gerekçe: siparişi alabilen/çözebilen biri onu
   okuyabilir.
2. İki yeni test: `orders.create` taşımayan bir oturumun her iki
   endpoint'ten de 403 aldığını kanıtlıyor.

## Out of scope

- `/staff`, `/waiter-load`, `/my-shift-summary`, `/handoff-note/pop`
  gibi bilinçli olarak "yalnız oturum yeterli" bırakılmış endpoint'ler
  — bunlar ayrı bir tasarım kararı (V1-RMD-177/212), bu görevin
  kapsamında değil.

## Dependencies

- V1-RMD-160

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Debug` → 0 uyarı, 0 hata.
- Gerçek Postgres'e karşı `tests/Host/Experience/Orders/TableDraft` →
  79/79 yeşil (2 yeni test dahil); revert-and-confirm ile her iki test
  de ayrı ayrı gerçekten kırılıp doğrulandı.

## Handoff

- None
