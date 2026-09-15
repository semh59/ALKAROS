# V1-RMD-212 - Kasiyer'in garson seçicisinde gerçek zamanlı yük göstergesi

- Task ID: V1-RMD-212
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

Semih'in "Ne kaldı şimdi" sorusunda erteleyip görev olarak açmadığı
ikinci madde: Kasiyer'in masa açarken kullandığı garson seçicisi
(V1-RMD-205) bugün yalnızca isim listesi ve tek bir "önerilen" işareti
gösteriyor — hangi garsonun şu an kaç açık masası olduğu görünmüyor.
Kasiyer, öneriyi kör kabul etmek yerine "Ayşe zaten 5 masaya bakıyor,
ben Mehmet'i seçeyim" gibi bilgiyle kendi kararını verebilmeli.

Yeni `GET /orders/waiter-load` uç noktası — `SuggestedWaiterResolver`'ın
zaten hesapladığı `active_load` sayısını (V1-RMD-202/208/V1-SET-006 ile
aynı LATERAL sorgu deseni), aday süzgeçlerinden (canlı oturum, tavan)
bağımsız olarak, `orders.send` taşıyan her aktif kullanıcı için döner —
kasiyer arayüzünde "arka planda" bir garsonun yükü de görünsün, yalnız
şu an ulaşılabilir olanların değil (görünürlük ile önerilebilirlik ayrı
sorular).

## Owned surface

- src/Host/Experience/Orders/SuggestedWaiterResolver.cs (paylaşılan
  dosya, V1-RMD-211'in de sahibi olduğu) — yeni
  `ListActiveLoadsAsync(ct)` metodu.
- src/Host/Experience/Orders/OrderManagementContracts.cs (paylaşılan
  dosya) — yeni `WaiterLoadV1(Guid UserId, int ActiveLoad)`.
- src/Host/Experience/Orders/OrderManagementEndpoints.cs (paylaşılan
  dosya) — yeni `GET /orders/waiter-load` (terminal-read rate limit,
  kasiyer oturumu yeterli — `/staff` ile aynı yetki seviyesi).
- src/Clients/Cashier/wwwroot/cashier-app.js (Sınırlı ek — V1-CUI-005/
  V1-RMD-051 sahipliğinde) — `loadWaiterOptions()` üçüncü bir paralel
  istek daha yapıyor; `renderWaiterOptions()` her seçeneğin yanına
  yük sayısını yazıyor (ör. "Ayşe (3 masa)").
- tests/Host/Experience/Orders/TableDraft/** (ilgili görev
  sahipliğinde) — yeni uç nokta için HTTP testleri.
- tests/Clients/StaticApps/cashier-app.test.js (ilgili görev
  sahipliğinde) — yeni fetch + render doğrulaması.

## In scope

1. `SuggestedWaiterResolver.ListActiveLoadsAsync(ct)`: aktif +
   `orders.send` taşıyan her kullanıcı için `(UserId, ActiveLoad)` —
   canlı oturum şartı YOK (görünürlük, ulaşılabilirlik değil), tavan
   şartı YOK (o, önerilebilirlik sorusu, bu görünürlük sorusu).
2. `GET /orders/waiter-load`: kasiyer oturumu (`RequireCashierSessionAsync`),
   `terminal-read` rate limit — `/staff` ile birebir aynı yetki modeli.
3. `cashier-app.js`: üçüncü paralel fetch, `state.waiterLoads` (userId →
   activeLoad map), `renderWaiterOptions()` etiketleri
   `"{isim} ({yük} masa)"` biçiminde (yük 0 ise yalnız isim — "0 masa"
   gürültü, sıfır zaten sessizliktir).

## Out of scope

- Canlı push/SignalR ile anlık güncelleme — sayfa her masa açılışında
  zaten yeniden `loadWaiterOptions()` çağırıyor (V1-RMD-205); bu,
  "neredeyse gerçek zamanlı" kabul edildi, ayrı bir WebSocket kanalı
  açmak bu görevin kapsamı dışında.
- WaiterPwa'nın `/transfer-server` seçicisi (`transfer.js`) — o ayrı bir
  UI, bu görev yalnız Kasiyer'in masa-açma seçicisini kapsıyor.

## Dependencies

- V1-RMD-211
- V1-SET-006

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Debug` → 0 uyarı, 0 hata.
- `node --check cashier-app.js` → temiz.
- Gerçek Postgres'e karşı `tests/Host/Experience/Orders/TableDraft` →
  tüm testler yeşil (yeni uç nokta testi dahil); revert-and-confirm ile
  en az bir yeni test gerçekten kırılıp doğrulanır.
- `tests/Clients/StaticApps` (`npx vitest run`) → tüm testler yeşil.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0
  uyarı.
- `python tools/consistency-audit/consistency_audit.py` → bu görevin
  değiştirdiği dosyalarda 0 ihlal.

## Handoff

- None
