# V1-RMD-177 - `/comp` ve `/transfer-server`'ın Garson istemcisi

- Task ID: V1-RMD-177
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: remediation
- Surface state: Existing

## Goal

`docs/engineering/garson-audit-2026-09-10.md`'nin API uç noktaları
bölümünde bilinçli olarak ertelenmiş son bulguyu kapatır: `/comp` ve
`/transfer-server` uç noktalarının hiçbir istemcisi yoktu. İkisi de gerçek
sunucu mantığı olan, tam test edilmiş uç noktalardı — eksik olan yalnızca
istemci tarafıydı, ama `/transfer-server` için kapsam beklenenden derin
çıktı (aşağıya bakın).

### `/comp` (ikram)

`/void-sent`'inkiyle birebir aynı desen (V1-IAM-027, V1-RMD-155): `bills.comp`
grant-class bir izin — doğrudan taşıyan bir rol (kasiyer/amir/yönetici)
uygular, taşımayan biri (garson) için istek yöneticiye gider ve 202
Pending dönebilir. Sunucu tarafı zaten vardı (V1-BIL-005) ve tam test
kapsamı altındaydı; hiçbir istemci hiç çağırmıyordu.

- Düzeltme: foundations.md §0.2'ye uyarak, `OrderItemDto`'ya
  `ItemExceptionHandler.ApplyComplimentaryAsync`'in kendi uygunluk
  kontrolünü (yalnız `Status == Active`; `void`'in aksine `KitchenState`'i
  hiç önemsemez) birebir yansıtan bir `CanComp` alanı eklendi —
  `CanVoid`/`CanVoidSent`'in V1-RMD-168'de aldığı yolun aynısı.
- `waiter-app.js`'e `openCompSheet`/`confirmComp` eklendi — `void-sent`
  akışının aynısı: aynı idempotency-key-bir-onaydan-sonra-tekrar-kullanma
  deseni, `COMP_REASONS` (server'ın `ComplimentaryReasonCatalog`'u).
  Satırdaki "İkram" düğmesi yalnız `item.canComp` true iken görünür.

### `/transfer-server` (vardiya devri)

Bunun hiç istemcisi olmamasının nedeni beklenenden derindi: hiçbir
kasiyer-seviyesi oturumun personel listesi görecek HİÇBİR yolu yoktu
(`/api/v1/management/users` yalnız POST — oluşturma — taşıyordu, GET/liste
hiç yoktu; o da zaten yönetici oturumu istiyordu). Bir devir hedefi seçmek
için önce yeni, dar kapsamlı bir liste ucu gerekti.

- `IRoleRepository`/`PostgresRoleRepository`'ye `ListActiveUsersAsync`
  eklendi (yalnız `user_id`+`display_name`, arayan hariç, aktif
  kullanıcılar).
- Yeni `GET /api/v1/terminals/{terminalId}/orders/staff` — herhangi bir
  kasiyer oturumundan erişilebilir (özel bir izin gerekmiyor; bir listeden
  meslektaş seçmek başlı başına ayrıcalıklı bir eylem değil —
  `TransferServingUserAsync` zaten kendi gerçek yetkilendirme kararını
  veriyor).
- `waiter-app.js`: Personel sheet'ine "Açık masaları devret" eklendi.
  Yalnızca `orders.transfer-server` (kendi masalarını devretme, HER rol
  taşıyor) kullanılıyor — `orders.transfer-server-any` (başkasının
  masalarını devretme) kasiyer/amir işi, bu istemcinin kapsamı dışında.

### Kapsam kararı

PosTerminal (kasiyer) tarafında ikisinin de istemcisi yok, ama audit
bulgusu "hiçbir istemci yok" idi — Garson tarafında bir istemci olması
bulguyu kapatır. Kasiyer zaten `bills.comp`'u doğrudan taşıdığı için
oradaki eksiklik daha düşük öncelikli; ayrı bir görev olabilir.

## Owned surface

- `plan/v1/remediation/V1-RMD-177-comp-and-transfer-server-clients.md` (yeni)
- Sınırlı ek:
  - src/Host/Experience/Orders/OrderManagementContracts.cs (Host sahipliğinde)
    — `OrderItemDto.CanComp`, yeni `StaffMemberV1` record.
  - src/Host/Experience/Orders/OrderManagementStore.cs (Host sahipliğinde)
    — `MapToDto`'nun `CanComp` hesaplaması.
  - src/Host/Experience/Orders/OrderManagementEndpoints.cs (Host sahipliğinde)
    — yeni `GET /staff`.
  - src/Modules/Identity/Authorization/IRoleRepository.cs,
    PostgresRoleRepository.cs (Identity sahipliğinde) — yeni
    `ListActiveUsersAsync`.
  - src/Clients/WaiterPwa/wwwroot/waiter-app.js, waiter-app.css
    (V1-WTR-010 sahipliğinde) — İkram düğmesi/sheet'i, devir sheet'i,
    `.btn-quiet` (V1-RMD-176'da ölü olarak silinmişti, şimdi gerçek bir
    tüketicisi var, geri geldi).
  - tests/Host/Experience/Orders/VoidSent/OrderManagementVoidSentHttpTests.cs
    (Host test sahipliğinde) — mevcut `CanVoidAndCanVoidSent...` testine
    `CanComp` doğrulaması eklendi.
  - tests/Host/Experience/Orders/TableDraft/OrderManagementTableDraftHttpTests.cs
    (Host test sahipliğinde) — yeni `/staff` testleri ve `GetRequest`
    yardımcısı (bağımsız incelemede düzeltildi: `OrderManagementTableDraft
    TestDatabase.cs` burada yanlışlıkla dokunulmuş olarak listelenmişti —
    o dosyaya hiç dokunulmadı).

## Out of scope

PosTerminal'in kendi `/comp`/`/transfer-server` istemcisi. `orders.transfer-
server-any` (başkasının masalarını devretme) UI'ı — kasiyer/amir işi.

## Dependencies

- V1-RMD-176

## Acceptance evidence

- `dotnet build src/Host/ALKAROS.Host.csproj -c Debug` → 0 uyarı, 0 hata.
- `node --check waiter-app.js` → temiz; CSS parantez dengesi 242/242.
- `ALKAROS_TEST_PG_PORT=55432 ALKAROS_TEST_PG_PASSWORD=postgres
  ALKAROS_KITCHEN_STATION_ID=test-station dotnet test`:
  - `tests/Host/Experience/Orders/TableDraft/*.csproj` → 51/51 yeşil
    (2 yeni `/staff` testi dahil: bir meslektaşı listeler ve arayanın
    kendisini hariç tutar; oturumsuz istek 401).
  - `tests/Host/Experience/Orders/VoidSent/*.csproj` → 14/14 yeşil
    (genişletilmiş `CanComp` doğrulaması dahil — üç kitchen-state'in
    ÜÇÜNDE de true, `CanVoid`/`CanVoidSent`'in aksine KitchenState'i hiç
    önemsemediğini kanıtlıyor).
  - `tests/Host/Experience/Orders/Comp/*.csproj` → 9/9 yeşil (regresyon
    yok — mevcut sunucu-tarafı comp testleri DTO değişikliğinden
    etkilenmedi).
  - `tests/Architecture/ModuleBoundaries/ALKAROS.Architecture.Tests.csproj`
    → 9/9 yeşil (yeni `IRoleRepository` kullanımı zaten onaylı bir modül
    kenarı — Orders Host Experience'ı bunu `/comp`'un grant akışı için
    zaten kullanıyordu).
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → bu görevin
  değiştirdiği dosyalarda 0 ihlal. (Kalan tek ihlal,
  `src/Modules/Inventory/ManualAdjustments/InventoryAdjustmentService.cs:96`,
  bu görevden önce vardı ve sahip olunan yüzeyin dışında.)

## Handoff

- None
