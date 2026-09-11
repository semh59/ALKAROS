# V1-RMD-168 - canVoid/canVoidSent artık sunucunun kendi alanı

- Task ID: V1-RMD-168
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: remediation
- Surface state: Existing

## Goal

`docs/engineering/garson-audit-2026-09-10.md`'nin Frontend bölümündeki
"beş §0 ihlali" listesinden birini kapatır: "`canVoid` istemcide
türetiliyor" (`docs/design/foundations.md` §0.2: "hangi aksiyon geçerli,
backend söyler — DTO'lar canX: bool gibi alanlar taşır, istemci koşulu
kendi yeniden üretmez").

`waiter-app.js`'in `renderSentLine`'ı `canVoid`/`canVoidSent`'i
`item.kitchenState` alanından kendi başına türetiyordu:
`canVoid = !kitchenSent`, `canVoidSent = kitchenSent && kitchenState
!== 'Served' && kitchenState !== 'Cancelled'`. Bu, gerçek iki uç noktanın
(`ItemExceptionHandler.VoidItemAsync`, `SentItemVoidStore.VoidAsync`)
kendi uygunluk kontrollerinin YARISINI (`KitchenState` kısmını) doğru
yakalıyordu ama `Status` kısmını (`Active`/`Draft` mi, `Cancelled`/`Waste`
mi) hiç görmüyordu — bugün zararsız, çünkü `activeItems()` zaten
Cancelled/Waste kalemleri render'a hiç sokmuyor, ama bu bir kural değil,
bir tesadüf; istemci kodunun kendisi bunu garanti etmiyordu.

Düzeltme:

- `OrderItemDto`'ya iki yeni alan: `CanVoid`, `CanVoidSent`
  (`src/Host/Experience/Orders/OrderManagementContracts.cs`).
- `OrderManagementStore.MapToDto`, bu iki alanı her iki handler'ın kendi
  uygunluk koşuluyla BİREBİR aynı ifadeyle hesaplıyor:
  - `CanVoid`: `Status ∈ {Active, Draft} ∧ KitchenState = NotSent`
    (`ItemExceptionHandler.VoidItemAsync`'in kendi kontrolü).
  - `CanVoidSent`: `Status = Active ∧ KitchenState ∉ {NotSent, Served,
    Cancelled}` (`SentItemVoidStore.VoidAsync`'in kendi kontrolü).
- `waiter-app.js`, artık `item.canVoid`/`item.canVoidSent`'i doğrudan
  okuyor — hiçbir koşulu kendi yeniden üretmiyor.

## Owned surface

- `plan/v1/remediation/V1-RMD-168-canvoid-server-derived.md` (yeni)
- Sınırlı ek:
  - src/Host/Experience/Orders/OrderManagementContracts.cs (V1-ORD-005
    sahipliğinde) — OrderItemDto'ya iki yeni alan.
  - src/Host/Experience/Orders/OrderManagementStore.cs (V1-RMD-147
    sahipliğinde) — MapToDto'da hesaplama.
  - src/Clients/WaiterPwa/wwwroot/waiter-app.js (V1-WTR-010 sahipliğinde)
    — sunucunun alanlarını okuyor, kendi türetmesini kaldırdı.
  - tests/Host/Experience/Orders/VoidSent/OrderManagementVoidSentHttpTests.cs
    (V1-IAM-027 sahipliğinde) — yeni test, GET yardımcıları.

## Out of scope

Frontend'in kalan "§0 ihlalleri" ve diğer bulguları — ayrı görev/görevler.

## Dependencies

- V1-RMD-167

## Acceptance evidence

- `dotnet build src/Host/ALKAROS.Host.csproj -c Debug` → 0 uyarı, 0 hata.
- `node --check src/Clients/WaiterPwa/wwwroot/waiter-app.js` → temiz.
- `ALKAROS_TEST_PG_PORT=55432 ALKAROS_TEST_PG_PASSWORD=postgres
  ALKAROS_KITCHEN_STATION_ID=test-station dotnet test` gerçek test
  Postgres'ine karşı:
  - `tests/Host/Experience/Orders/VoidSent` — **14/14 yeşil** (13 mevcut +
    1 yeni: `CanVoidAndCanVoidSentMatchEachHandlersOwnEligibilityCheck`,
    üç mutfak durumunu — NotSent/Preparing/Served — gerçek `GET
    /{orderId}` üzerinden okuyup beklenen canVoid/canVoidSent
    kombinasyonlarını doğruluyor).
  - `tests/Host/Experience/Orders/{Comp,Confirmation,Void,TableDraft}` —
    toplam 82/82 yeşil.
- Yeni testin **vacuous olmadığı kanıtlandı** — hem de en güçlü şekilde:
  `git stash` ile `OrderManagementContracts.cs`/`OrderManagementStore.cs`
  değişiklikleri geri alınınca test dosyası **derlenmedi bile**
  (`CanVoid`/`CanVoidSent` temel sürümde hiç yok) — çalışma zamanı
  başarısızlığından daha güçlü bir kanıt. Değişiklikler geri yüklendi,
  tekrar 14/14 yeşil.
- **Önemli ortam notu (bu görevi doğrularken bulundu, ayrı bir kusur
  değil):** `ALKAROS_KITCHEN_STATION_ID` ortam değişkeni verilmezse
  `AddOrderManagementExperience()` kullanan HER test host'u, endpoint'in
  kendi hata eşleyicisine hiç ulaşmadan, gövdesiz çıplak bir 500 döner —
  yalnızca daha önce bilinen `ProductionExperienceCompositionTests`'e
  özgü değilmiş. `docs/engineering` hafızasına not edildi
  (ui-redesign-foundations.md).
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → bu görevin
  değiştirdiği dosyalarda 0 ihlal. (Kalan tek ihlal,
  `src/Modules/Inventory/ManualAdjustments/InventoryAdjustmentService.cs:96`,
  bu görevden önce vardı ve sahip olunan yüzeyin dışında.)

## Handoff

- None
