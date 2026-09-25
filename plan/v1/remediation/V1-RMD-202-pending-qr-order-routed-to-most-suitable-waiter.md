# V1-RMD-202 - Bekleyen QR siparişi artık en uygun garsona yönlendiriliyor

- Task ID: V1-RMD-202
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

Semih'in isteği (2026-09-15): "müşteri QR'dan sipariş veriyorsa en uygun
garsona bilgi geçsin". `SignalRPendingOrderAnnouncer`'ın kendi doc-comment'i
V1-RMD-149'dan beri aynı gerekçeyi taşıyor — "sistemde hangi garsonun hangi
masaya baktığı hiç kaydedilmiyor, bu yüzden hedeflenecek bir grup yok" — ve
bu artık yanlış: V1-RMD-201, `WaiterOrderStatusHub`'a UserId bazlı grup
katılımını ve `WebPushSender.SendToUserAsync`'i zaten ekledi. Bu görev aynı
altyapıyı `OrderPendingConfirmation` yoluna da bağlar, ama hedef kullanıcıyı
`Order.ServingUserId`'den değil (yeni bir sipariş için henüz yok) yeni bir
"en uygun garson" seçimiyle bulur.

"En uygun" tanımı (Semih'in kararı, 2026-09-15 sohbeti):

1. Adayın oturumu açık olmalı (`identity.device_sessions`, süre az önce
   V1-IAM-031 ile 8 saate düşürüldü) VE `orders.send` iznini taşımalı.
2. Adaylar arasından en az aktif (kapanmamış) siparişi olan seçilir —
   `orders.orders.serving_user_id` üzerinden, `ShiftSummaryStore`'un zaten
   kullandığı sayım deseninin aynısı.
3. Eşitlik durumunda en uzun süredir yeni sipariş almayan (adalet/rotasyon)
   öne geçer — yeni bir zaman damgası alanı gerektirmez, `MAX(serving_user_id
   = X olan siparişlerin created_at'i)` üzerinden türetilir.
4. Hiç uygun aday yoksa (kimsenin oturumu açık değil) önceki davranışa
   (herkese broadcast) düşülür — sessiz bir kayıp yerine güvenli bir
   varsayılan.

## Owned surface

- src/Host/Experience/PendingOrderNotifications/SignalRPendingOrderAnnouncer.cs
  (V1-RMD-149 sahipliğinde, sınırlı ek) — yeni `NpgsqlDataSource` bağımlılığı,
  yeni `ResolveMostSuitableWaiterAsync` sorgusu; `AnnounceAsync` artık
  bulunursa `Clients.Group`/`SendToUserAsync`, bulunamazsa eskisi gibi
  `Clients.All`/`BroadcastAsync` kullanıyor.
- tests/Host/Experience/PendingOrderNotifications/** (yeni test dosyaları;
  bu dizin hiçbir görev tarafından önceden sahiplenilmemiş, plan-audit'te
  çakışma yok) — `SignalRPendingOrderAnnouncer`'ın hedefleme mantığı için
  gerçek Postgres'e karşı testler.

## In scope

1. `ResolveMostSuitableWaiterAsync(CancellationToken)`: tek bir SQL
   sorgusuyla (identity.users + user_roles + role_permissions + permissions
   - device_sessions + orders.orders) en uygun garsonun `user_id`'sini
   (veya adayı yoksa `null`) döndürür.
2. `AnnounceAsync`: hedef bulunursa SignalR `Clients.Group(WaiterOrderStatusHub
   .GroupName(id))` ve push `SendToUserAsync`; bulunamazsa mevcut
   `Clients.All`/`BroadcastAsync` davranışı aynen korunur.
3. Sorgu yalnız `orders.send` iznini taşıyan, `active=true` kullanıcıları ve
   süresi geçmemiş/iptal edilmemiş bir `device_sessions` satırı olanları
   kapsar.

## Out of scope

- Masa-garson devamlılığı (aynı masaya daha önce bakan garson tercih
  edilsin) — V1-RMD-201'in out-of-scope kararıyla aynı gerekçe: kalıcı bir
  atama modeli bu görevin kapsamında değil.
- `OrderItemReady` (mutfaktan "hazır" bildirimi) — o zaten V1-RMD-201'de
  `ServingUserId`'ye hedefleniyor, bu görev yalnız `OrderPendingConfirmation`
  yolunu (henüz `ServingUserId`'si olmayan yeni QR siparişi) kapsıyor.
- PosTerminal/Cashier istemci değişikliği — sunucu tarafı hedefleme,
  istemciler hangi kanaldan geldiğini ayırt etmiyor.

## Dependencies

- V1-RMD-201
- V1-IAM-031

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Debug` → 0 uyarı, 0 hata.
- Gerçek Postgres'e karşı yeni test projesi → tüm testler yeşil: oturumu
  açık + en az yüklü garson seçiliyor, oturumu kapalı/izni olmayan biri asla
  seçilmiyor, eşitlikte en uzun süredir sipariş almayan öne geçiyor, hiç
  aday yokken eskisi gibi herkese broadcast ediliyor (revert-and-confirm ile
  doğrulanır).
- `tests/Host/Experience/Orders/Confirmation/*.csproj`,
  `tests/Host/Experience/NfcOrdering/*.csproj`: regresyon yok.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → bu görevin
  değiştirdiği dosyalarda 0 ihlal.
- Semih'in elle deneyebileceği senaryo: iki garson hesabıyla oturum aç,
  birinin üzerinde birkaç açık sipariş bırak, diğerini boş tut; bir masanın
  QR menüsünden sipariş ver — bildirim yalnız boş/az yüklü garsonun
  cihazında çıkar, diğerinde çıkmaz. İkisinin de oturumunu kapat, tekrar
  QR'dan sipariş ver — eskisi gibi (varsa) her bağlı cihazda bildirim çıkar.

## Handoff

- None
