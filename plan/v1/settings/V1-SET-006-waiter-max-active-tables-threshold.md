# V1-SET-006 - `waiter.max_active_tables` ayarlanabilir yük tavanı

- Task ID: V1-SET-006
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

Semih'in "Ne kaldı şimdi" sorusunda erteleyip görev olarak açmadığı iki
maddeden biri: `SuggestedWaiterResolver.ResolveMostSuitableWaiterAsync`
(V1-RMD-202/204/208) şu an her zaman en düşük yüklü garsonu önerir/atar,
ama hiçbir üst sınır yok — teoride tek bir garson açıksa (oturum canlı),
o kişi kaç masası olursa olsun önerilmeye devam eder. `waiter.max_active_tables`
adında yeni bir `WholeNumber` ayar: bir garsonun `active_load`'u bu
değere ulaştığında/aştığında artık aday havuzuna girmez (öneri de,
`/table-draft`'ın `IsValidWaiterAsync` atama-doğrulaması da değil —
o ayrı bir soru, "bu kullanıcı gerçek bir garson mu", kapasite değil).
Varsayılan değer `0` = sınır yok (bu görevden önceki davranışla birebir
aynı) — hiçbir deployment bu ayarı elle açmadan davranış değişmez,
`KitchenDenseModeThresholdSetting` (V1-SET-005) emsaliyle aynı ilke.

## Owned surface

- `src/Modules/Settings/WaiterMaxActiveTables/**` (yeni)
- `tests/Modules/Settings/WaiterMaxActiveTables/**` (yeni)

Sınırlı ek (yollar geri-tik olmadan, V1-RMD-111 emsali):

- src/Host/Experience/Orders/SuggestedWaiterResolver.cs (paylaşılan
  dosya) — yeni `ISettingsService` bağımlılığı; aday sorgusu artık
  `@max_active_tables = 0 OR COALESCE(w.active_load,0) < @max_active_tables`
  filtresi taşıyor.
- tests/Host/Experience/PendingOrderNotifications/** (ilgili görev
  sahipliğinde) — yeni `SuggestedWaiterResolver` çağrıları
  `ISettingsService` alıyor; settings şema fixture'ı (026-typed-settings)
  csproj'a eklendi; tavan davranışını doğrulayan yeni test.
- ALKAROS.slnx (paylaşılan dosya) — yeni test projesi eklendi; ayrıca
  `python tools/project-manifest/project_manifest_tool.py` çalıştırılırken
  bulunan, bu görevden önceye ait (V1-RMD-202) bir kök neden düzeltildi:
  `tests/Host/Experience/PendingOrderNotifications` csproj'u hiç
  slnx'e eklenmemişti (disk_missing_in_slnx) — eklendi.

## In scope

1. `WaiterMaxActiveTablesSetting` (`KitchenDenseModeThresholdSetting` ile
   aynı kalıp): `Key = "waiter.max_active_tables"`, varsayılan `0`,
   `GetLimitAsync(ISettingsService, ct)` ilk çağrıda kendini kaydeder.
2. `SuggestedWaiterResolver` constructor'ı `ISettingsService settings`
   alıyor; `ResolveMostSuitableWaiterAsync` sorgusunun `WHERE`'ine tavanı
   ekliyor. `IsValidWaiterAsync` değişmedi (kapasite değil kimlik sorusu).
3. Test projesine `026-typed-settings.up.sql` fixture'ı eklenip yeni bir
   test: tavan aşıldığında o garson önerilmiyor, tavan `0`'dayken (veya
   hiç ayarlanmamışken) eskisi gibi davranıyor.

## Out of scope

- Ayarı değiştirmek için özel bir HTTP yüzeyi — `KitchenDenseModeThresholdSetting`
  emsalinde olduğu gibi (V1-SET-005 → V1-KIT-013 ayrı görevdi), bu
  ayar için de gerekirse ayrı bir takip görevi açılır. Operatör bu
  ayarı şimdilik doğrudan `settings.typed_settings` tablosu üzerinden
  değiştirir (diğer henüz-yüzeysiz ayarlarla aynı durum).
- Gerçek zamanlı yük göstergesi UI'ı — ayrı görev (V1-RMD-213 vb.).

## Dependencies

- V1-RMD-210

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Debug` → 0 uyarı, 0 hata.
- Gerçek Postgres'e karşı `tests/Modules/Settings/WaiterMaxActiveTables`
  ve `tests/Host/Experience/PendingOrderNotifications` → tüm testler
  yeşil; revert-and-confirm ile tavan testinin gerçekten tavana bağlı
  olduğu kanıtlanır.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0
  uyarı.
- `python tools/consistency-audit/consistency_audit.py` → bu görevin
  değiştirdiği dosyalarda 0 ihlal.
- `python tools/project-manifest/project_manifest_tool.py` → VALID (0
  fark) — yeni test projesi dahil, PendingOrderNotifications'ın
  eski eksik slnx kaydı da dahil.

## Handoff

- None
