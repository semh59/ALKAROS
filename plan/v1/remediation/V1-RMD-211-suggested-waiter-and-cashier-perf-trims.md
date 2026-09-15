# V1-RMD-211 - Bölge sorgusu tek sorguya katlandı, Cashier'ın iki fetch'i paralel

- Task ID: V1-RMD-211
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

Bağımsız `/code-review` denetiminin (2026-09-15) bulduğu iki küçük,
düşük riskli performans bulgusu:

1. `SuggestedWaiterResolver.ResolveMostSuitableWaiterAsync`, hedef
   masanın bölgesini ayrı bir `ResolveZoneIdAsync` sorgusuyla önceden
   okuyordu, hâlbuki ana sorgu zaten `table_mgmt.tables`'a bağlanıyor —
   gerçek bir masaya bağlı her öneri (QR yolu) iki ardışık round-trip
   ödüyordu, bir yerine.
2. `cashier-app.js`'in `loadWaiterOptions()`'ı `/orders/staff` ve
   `/orders/suggested-waiter`'ı ardışık `await`'liyordu — ikisi de
   bağımsız, kendi hatasını yutan GET istekleri, sıralı çalışmalarının
   hiçbir nedeni yok.

## Owned surface

Sınırlı ek (yollar geri-tik olmadan, V1-RMD-111 emsali):

- src/Host/Experience/Orders/SuggestedWaiterResolver.cs (paylaşılan
  dosya) — bölge okuma artık ana sorgunun kendi `LEFT JOIN`'ü; ayrı
  `ResolveZoneIdAsync` kaldırıldı.
- src/Clients/Cashier/wwwroot/cashier-app.js (Sınırlı ek — V1-CUI-005/
  V1-RMD-051 sahipliğinde) — `loadWaiterOptions()` artık iki isteği
  `Promise.all` ile paralel yapıyor.

## In scope

1. `ResolveMostSuitableWaiterAsync`'in tek SQL'i artık
   `table_mgmt.tables`'ı doğrudan `LEFT JOIN (... WHERE table_id =
   @table_id) ON true` ile hedef bölgeyi de okuyor — `@table_id::uuid`
   NULL'sa (Cashier) join'in kendisi sıfır satır dönmez (`LEFT JOIN`),
   yalnız `zone_id`'si NULL olur, bölge kademesi eskisi gibi devre dışı
   kalır.
2. `cashier-app.js`: iki `fetch` çağrısı `Promise.all`'a alındı; ikisi
   de zaten kendi hatasını yutuyordu (try/catch), davranış değişmedi.

## Out of scope

- `WaiterPresenceTracker.Disconnected`'ın atomik-silme deyimi — denetimin
  en düşük öncelikli, salt okunabilirlik bulgusu; kod zaten doğru ve
  yorumlu, ayrı bir görev gerektirmeyecek kadar küçük.

## Dependencies

- V1-RMD-208

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Debug` → 0 uyarı, 0 hata.
- `node --check cashier-app.js` → temiz.
- Gerçek Postgres'e karşı `tests/Host/Experience/PendingOrderNotifications`
  → tüm testler yeşil (bölge testleri dahil, davranış değişmedi).
- `tests/Clients/StaticApps` (`npx vitest run`) → tüm testler yeşil.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0
  uyarı.
- `python tools/consistency-audit/consistency_audit.py` → bu görevin
  değiştirdiği dosyalarda 0 ihlal.

## Handoff

- None
