# V1-RMD-207 - `AssignedWaiterUserId` gerçek bir garson mu, doğrulanıyor

- Task ID: V1-RMD-207
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

V1-RMD-204'ün kendi doğrulaması sırasında (Semih'in isteğiyle) bulunan
gerçek bir boşluk: `/table-draft`'ın `AssignedWaiterUserId`'si,
`orders.transfer-server-any` taşıyan bir çağıran için kabul ediliyordu
ama hedefin gerçekten var olan, aktif, `orders.send` taşıyan bir
kullanıcı olup olmadığı hiç kontrol edilmiyordu — `orders.orders.
serving_user_id`'nin FK'sı olmadığı için (V1-RMD-111'in bilinçli modül
sınırı kararı) rastgele/hatalı bir id sessizce kabul ediliyor, sipariş
var olmayan birine atanmış gibi görünüyordu.

## Owned surface

Sınırlı ek (yollar geri-tik olmadan, V1-RMD-111 emsali):

- src/Host/Experience/Orders/SuggestedWaiterResolver.cs (paylaşılan
  dosya) — yeni IsValidWaiterAsync(userId, ct): kullanıcı aktif mi,
  orders.send taşıyor mu (V1-RMD-202'nin adaylık sorgusuyla aynı iki
  koşuldan ilki, oturum şartı olmadan — atanan garsonun o an oturum
  açık olması gerekmiyor, yalnız gerçek ve yetkili olması gerekiyor).
- src/Host/Experience/Orders/OrderManagementEndpoints.cs (Orders
  Management sahipliğinde) — /table-draft artık AssignedWaiterUserId
  doluysa (ve kendisi değilse) önce IsValidWaiterAsync ile doğruluyor,
  geçersizse 400.
- tests/Host/Experience/Orders/TableDraft/** (ilgili görev
  sahipliğinde) — yeni doğrulama senaryoları.

## In scope

1. `IsValidWaiterAsync`: `identity.users.active` VE `orders.send` izni —
   oturum şartı yok (bu, "şu an ulaşılabilir mi" sorusu değil, "bu
   gerçekten var olan bir garson mu" sorusu).
2. `/table-draft`: `AssignedWaiterUserId` doluysa ve `actingUserId`'den
   farklıysa, `orders.transfer-server-any` kontrolünden SONRA (izin
   kontrolü önce — kimin isteyebileceği kimin var olduğundan önce
   sorulur) `IsValidWaiterAsync` çağrılır; `false` ise `400 INVALID_
   WAITER` — Türkçe mesaj (docs/UI_STYLE_GUIDE.md).

## Out of scope

- Bölge farkındalığı (öneri sıralaması) — V1-RMD-208.
- `transfer-server` uç noktasının kendi hedef doğrulaması — o zaten
  `TransferServingUserAsync`'in kendi `InvalidTransferTargetException`'ı
  ile korunuyor (V1-RMD-111), bu görevin kapsamında değil.

## Dependencies

- V1-RMD-204

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Debug` → 0 uyarı, 0 hata.
- Gerçek Postgres'e karşı `tests/Host/Experience/Orders/TableDraft` →
  tüm testler yeşil (yeni: var olmayan bir id'ye atama 400 döner,
  gerçek ama `orders.send` taşımayan birine atama 400 döner, gerçek ve
  yetkili birine atama eskisi gibi çalışır); revert-and-confirm ile en
  az bir yeni test gerçekten kırılıp doğrulanır.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0
  uyarı.
- `python tools/consistency-audit/consistency_audit.py` → bu görevin
  değiştirdiği dosyalarda 0 ihlal.
- Semih'in elle deneyebileceği senaryo: `orders.transfer-server-any`
  taşıyan bir kasiyer oturumuyla rastgele bir GUID'i
  `AssignedWaiterUserId` olarak gönder — 400 döner, sipariş oluşmaz.

## Handoff

- None
