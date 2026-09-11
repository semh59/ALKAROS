# V1-WTR-013 - Vardiya devrinde bağlam notu

- Task ID: V1-WTR-013
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

`garson-karsilastirma` karşılaştırma dokümanının "Yeni fikirler" bölümünde
(Katman A, madde 2) önerilen ikinci özellik. V1-RMD-177'de eklenen
`/transfer-server` yalnız kim-kime taşındığını taşıyor, NEDEN'i taşımıyordu
— devreden garson "5 nolu masa tatlı bekliyor, 8 şikayetçiydi" gibi bir
bağlamı devralana hiçbir şekilde aktaramıyordu. Semih'in onayladığı
parametreler: en fazla 200 karakter, devralan ilk masayı açtığında bir kez
gösterilir.

Düzeltme: `TransferServingUserRequestV1`'e opsiyonel `HandoffNote` eklendi.
Not, kendi küçük tablosunda (`notifications.serving_handoff_notes`) tutuluyor
— bir siparişin alanı değil, çünkü bir DEVİR OLAYINA ait (bir gönderen, bir
alan, bir an), herhangi bir tek siparişe değil; her devredilen sipariş
üzerinde saklansaydı kalıcı sipariş geçmişine sızardı ve "görüldü" diye
işaretlenecek doğal bir yer olmazdı. `notifications` şemasında,
`push_subscriptions`'ın (V1-WTR-011) yanında yaşıyor — aynı tür şey: küçük,
geçici, kullanıcıya yönelik operasyonel meta veri, hiçbir domain
agregatının parçası değil.

Yeni `POST .../orders/handoff-note/pop` uç noktası notu TEK bir sorguda
okuyup TÜKETİLMİŞ olarak işaretliyor (ayrı bir "okundu" çağrısı yok, yarış
durumu yok) — `waiter-app.js`'nin `openTable()`'ı her masa açılışında bunu
çağırıyor, ama not en fazla bir kez var olduğu için pratikte yalnız
devralındıktan sonraki İLK masa açılışında bir şey döner, sonrasındaki her
çağrı boş döner. `LeaveAsync`, aynı alıcı için önceki tüketilmemiş bir notu
sessizce süper ediyor (en yeni not her zaman kazanır) — bir garson masayı
açmadan iki devir alırsa yalnız en yeniyi görür.

## Owned surface

- `plan/v1/waiter-pwa/V1-WTR-013-serving-handoff-note.md` (yeni)
- `database/migrations/V1/V1-WTR-013/**` (yeni)
- `src/Host/Experience/Orders/ServingHandoffNoteStore.cs` (yeni)
- Sınırlı ek:
  - src/Host/Experience/Orders/OrderManagementContracts.cs,
    OrderManagementEndpoints.cs (Host sahipliğinde) —
    `TransferServingUserRequestV1.HandoffNote`, yeni
    `ServingHandoffNoteV1`, `/transfer-server`'ın not bırakması, yeni
    `POST /handoff-note/pop`, `HandoffNoteTooLongException` eşlemesi, DI
    kaydı.
  - src/Clients/WaiterPwa/wwwroot/waiter-app.js, waiter-app.css
    (V1-WTR-010 sahipliğinde) — devir sheet'ine not alanı, `openTable()`'ın
    `popHandoffNoteIfAny()` çağrısı, genel `.hint` kuralı (önceden yalnız
    `.login-card` kapsamındaydı).
  - database/MigrationComposition/order.json — 100 kaydı.
  - src/Host/Composition/Migrations/MigrationManifest.cs — PhaseBMax.
  - tests/Host/MigrationComposition/Manifest/ManifestTests.cs — manifest
    testinin sınır değerleri.
  - tests/Host/Experience/Orders/TableDraft/OrderManagementTableDraftHttpTests.cs,
    ALKAROS.Host.Experience.Orders.TableDraft.Tests.csproj (Host test
    sahipliğinde) — yeni testler, yeni yardımcılar
    (`HandoffNotePopPath`/`PostRequest`), migrasyon 096+100'ün test
    fixture'ına eklenmesi (096 zaten gerekliydi — `notifications` şemasını
    o yaratıyor, bu proje daha önce hiç eklememişti).
  - tools/consistency-audit/consistency_audit.py,
    docs/CONSISTENCY_AUDIT.md — `HOST_AREA_EXTRA_SCHEMAS`'a
    `Experience/Orders` → `notifications` istisnası (`table_mgmt` pointer
    deseniyle aynı, belgeli bir istisna — kör nokta değil); aynı düzenlemede
    dokümandaki önceden eksik olan WebPush→`notifications` satırı da
    eklendi (bu görevden önce vardı, fırsatçı küçük düzeltme).

## Out of scope

Diğer "Katman A" fikirleri ("yardım çağır" sinyali, taslakta fiyat/stok
değişti işareti) — ayrı görevler.

## Dependencies

- V1-RMD-111
- V1-RMD-177
- V1-WTR-011
- V1-WTR-012

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Debug` (tüm çözüm) → 0 uyarı, 0 hata.
- `node --check waiter-app.js` → temiz; `waiter-app.css` parantez dengesi
  244/244.
- `ALKAROS_TEST_PG_PORT=55432 ALKAROS_TEST_PG_PASSWORD=postgres
  ALKAROS_KITCHEN_STATION_ID=test-station dotnet test`:
  - `tests/Host/Experience/Orders/TableDraft/*.csproj` → 56/56 yeşil
    (5 yeni test: not bırakıp bir kez tüketmek, hiç not bırakılmazsa
    tüketilecek bir şey olmaması, bekleyen not yokken 204, 200 karakter
    üstü reddedilir, ikinci not ilkini süper eder).
  - `tests/Architecture/ModuleBoundaries/ALKAROS.Architecture.Tests.csproj`
    → 9/9 yeşil.
  - `tests/Host/MigrationComposition/ALKAROS.Host.Tests.csproj --filter
    "FullyQualifiedName~ManifestTests"` → 16/16 yeşil.
  - `tests/Host/MigrationComposition/ALKAROS.Host.Tests.csproj --filter
    "FullyQualifiedName~DualScreenAuthorizationHttpTests"` → 5/5 yeşil.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → bu görevin
  değiştirdiği dosyalarda 0 ihlal. Bir gerçek ihlal bulundu ve düzeltildi:
  `Experience/Orders`'ın `notifications` şemasına yazması denetleyici
  tarafından yakalandı (`table_mgmt` pointer istisnasıyla aynı sınıf, kör
  nokta değil) — `HOST_AREA_EXTRA_SCHEMAS`'a belgeli bir istisna olarak
  eklendi, atlanmadı. Kalan tek ihlal,
  `src/Modules/Inventory/ManualAdjustments/InventoryAdjustmentService.cs:96`,
  bu görevden önce vardı ve sahip olunan yüzeyin dışında.

## Handoff

- None
