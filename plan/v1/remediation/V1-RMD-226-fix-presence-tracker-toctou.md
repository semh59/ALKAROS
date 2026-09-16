# V1-RMD-226 - `WaiterPresenceTracker.Disconnected`'daki TOCTOU penceresi kapatıldı

- Task ID: V1-RMD-226
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

Bağımsız çok-ajanlı denetimin (2026-09-16) bulduğu **LOW** bulgu
(V1-RMD-211'de "ayrı bir görev gerektirmeyecek kadar küçük" diye
kapsam dışı bırakılmıştı, bu oturumda "hepsini düzelt" talimatıyla ele
alındı): `Disconnected(userId)`, biri sayacı azaltan diğeri sıfıra
ulaşınca girdiyi silen İKİ AYRI atomik `ConcurrentDictionary` işlemiydi
— her biri kendi başına atomikti ama ikisi birlikte değildi. Aradaki
dar pencerede sözlük `{userId: 0}` tutuyordu ve eski `IsConnected`
(salt `ContainsKey`) bunu "hâlâ bağlı" diye okuyordu — hedefli bir
bildirim, artık bağlı olmayan bir kullanıcının grubuna gidip fallback
broadcast'e düşmeyebiliyordu.

## Owned surface

- src/Host/Experience/WaiterNotifications/WaiterPresenceTracker.cs
  (ilgili modülün sahipliğinde)
- tests/Host/Experience/WaiterNotifications/WaiterPresenceTrackerTests.cs
  (aynı modül)

## In scope

1. `Disconnected`: artık yalnızca sayacı azaltan TEK bir atomik
   `AddOrUpdate` — girdiyi hiç silmiyor. Sıfıra düşen bir girdi
   sözlükte kalıcı olarak durur (kullanıcı başına bir kerelik, sınırlı
   bir bellek maliyeti — sınırsız büyüme değil, çünkü kullanıcı sayısı
   zaten sabit/küçük).
2. `IsConnected`: artık `ContainsKey` değil, `TryGetValue(...) &&
   count > 0` — sıfır değerli bir girdi asla "bağlı" olarak
   okunmuyor, pencere tamamen ortadan kalkıyor.
3. Yeni test: aynı kullanıcı üzerinde tekrarlı bağlan/kopar
   döngülerinin (sıfır değerli girdiyi yeniden kullanarak) hiçbir
   zaman yanlışlıkla "bağlı" okunmadığını kanıtlıyor.

## Out of scope

- Yok — bu, V1-RMD-211'in bilinçli olarak ertelediği tam ve tek
  bulguydu.

## Dependencies

- V1-RMD-203

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Debug` → 0 uyarı, 0 hata.
- Gerçek Postgres'e karşı `tests/Host/Experience/WaiterNotifications`
  → 11/11 yeşil (yeni test dahil); revert-and-confirm ile gerçekten
  kırılıp doğrulandı.
- Regresyon taraması: `tests/Host/Experience/PendingOrderNotifications`
  → 10/10 yeşil.

## Handoff

- None
