# V1-RMD-135 - Independent audit: WaiterPwa masa tutarı ve ölü Cashier "Engine" sınıfları

- Task ID: V1-RMD-135
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

Bağımsız denetimin (2026-09-09) iki kalan bulgusu — biri V1-RMD-129'da
bilinçli olarak kapsam dışı bırakılmış bir tasarım kararı, diğeri
mimari bulguların son ikisi — kullanıcıyla ("Önerin ne" → "Evet")
görüşülüp onaylandı:

1. **WaiterPwa masa tutarı (V1-RMD-129'un kapsam dışı bıraktığı #12).**
   `TableDto` (`TableManagementContracts.cs`) o anki siparişin tutarını
   hiç taşımıyordu — `waiter-app.js` var olmayan `currentAmount`/`amount`
   alanlarını okuyup her zaman `0`'a düşüyor, masa listesi her zaman
   "Boş" gösteriyordu. Çözüm: uydurma bir alan ya da düzeltilmemiş bir
   durum yerine, `TableDto`'ya gerçek bir `CurrentOrderTotal` alanı
   eklendi — `Order.Total`'ın kendi formülüyle birebir aynı (Draft/Active
   durumundaki kalemlerin `gross_amount` toplamı; Cancelled/Waste/
   Complimentary hariç), doğrudan SQL ile hesaplanıyor.
2. **`OperationsStatusEngine` + `CashierShellEngine`.** Denetimde
   bulunan, önceki oturumlarda silinen `OrderEntryEngine`/
   `WaiterOfflineQueueEngine`/`MenuRecipeAdminEngine`/
   `InventoryPurchasingEngine`/`ProductionBatchEngine` ile aynı sınıf:
   DI'sız, saf bellek-içi C# "domain controller" — gerçek bir HTTP
   istemcisi yok, hiçbir sayfaya bağlı değil, yalnız kendi Models
   dosyasından ve kendi test projesinden referans alınıyor. Grep ile
   `src/Clients/Cashier/` ve daha geniş kapsamda sıfır dış tüketici
   doğrulandı; silindi.

## Owned surface

- `plan/v1/remediation/V1-RMD-135-waiterpwa-table-amount-and-dead-cashier-engines.md`
  (yeni)
- Sınırlı ek — aşağıdaki yollar ilgili görevlerin sahipliğinde kalır
  (yollar geri-tik olmadan yazıldı ki denetleyici bunları sahiplik
  iddiası olarak parse etmesin):
  - src/Clients/Cashier/OperationsStatus/** (V1-CUI-003 sahipliğinde),
    src/Clients/Cashier/TableShell/** (V1-CUI-001 sahipliğinde),
    tests/Clients/Cashier/OperationsStatus/**, tests/Clients/Cashier/
    TableShell/** — bu görevin kendi silme kararıyla tamamen kaldırıldı
    (grep ile doğrulandı: OperationsStatusEngine/CashierShellEngine ve
    Models/State/ViewModel companion dosyalarının src/Clients/Cashier/
    dışında ve o dizinlerin kendi test projeleri dışında sıfır gerçek
    tüketicisi vardı).
  - src/Host/Experience/Tables/TableManagementStore.cs (V1-RMD-026
    sahipliğinde) — yeni CurrentOrderTotalSql sabiti eklendi;
    GetAsync/GetAllAsync artık (Table, decimal CurrentOrderTotal) tuple
    döndürüyor. Diğer tüm metotlar (CreateAsync/UpdateAsync/
    ChangeStatusAsync) değişmedi.
  - src/Host/Experience/Tables/TableManagementContracts.cs (V1-IAM-024
    sahipliğinde) — TableDto'ya varsayılan 0m ile geriye dönük uyumlu
    yeni CurrentOrderTotal alanı eklendi; TableContractMapper.ToDto 4.
    (varsayılanlı) parametre aldı.
  - src/Host/Experience/Tables/TableManagementApplication.cs (V1-IAM-024
    sahipliğinde) — yalnızca GET /tables ve GET /tables/{tableId}
    handler'ları yeni tuple şeklini ToDto'ya iletecek şekilde
    güncellendi; diğer tüm handler'lar değişmedi.
  - tests/Host/Experience/Tables/TableManagementHttpTests.cs (V1-RMD-013
    sahipliğinde) — yeni SeedCurrentOrderWithItemsAsync yardımcı metodu
    ve yeni test TableListAndDetailReportTheCurrentOrdersRealRunningTotal
    eklendi; var olan hiçbir test değiştirilmedi.
  - src/Clients/WaiterPwa/wwwroot/waiter-app.js (V1-WTR-008/V1-RMD-051/
    V1-RMD-129 sahipliğinde) — yalnızca loadTables'taki
    amount: t.currentOrderTotal || 0 satırı ve ona ait yorum
    değiştirildi; dosyanın geri kalanına dokunulmadı.
  - ALKAROS.slnx (çözüm dosyası, paylaşılan) — silinen iki test projesi
    girişi (ALKAROS.Cashier.OperationsStatus.Tests,
    ALKAROS.Cashier.TableShell.Tests) kaldırıldı.

## In scope

1. `TableManagementStore.CurrentOrderTotalSql`: `orders.order_items`
   üzerinde `t.current_order_id`'ye bağlı, `status IN ('Draft',
   'Active')` filtreli `SUM(gross_amount)`, sipariş/kalem yoksa
   `COALESCE(..., 0)`. `Order.Total`'ın kendi domain formülüyle aynı;
   `gross_amount` kalıcı bir `NUMERIC(18,2)` kolonu olduğundan tam
   `Order` aggregate'ini yüklemeden doğrudan SQL ile güvenli.
2. `GetAsync`/`GetAllAsync` artık `(Table, decimal CurrentOrderTotal)`
   tuple döndürüyor; `TableManagementApplication.cs`'deki iki çağrı
   sitesi (`GET /tables`, `GET /tables/{tableId}`) buna göre
   güncellendi. Create/update/status-change gibi diğer `ToDto` çağrı
   yerleri incelendi: bunlarda henüz bir "o anki sipariş" olmadığından
   ya da WaiterPwa zaten her aksiyon sonrası listeyi yeniden çektiğinden
   3 parametreli overload (varsayılan `0m`) kasıtlı olarak bırakıldı.
3. `TableDto.CurrentOrderTotal` (varsayılan `0m`) — geriye dönük uyumlu,
   var olan hiçbir tüketiciyi bozmuyor.
4. `waiter-app.js`: `amount: t.currentOrderTotal || 0` — artık gerçek
   backend alanını okuyor.
5. `OperationsStatusEngine`/`CashierShellEngine` ve ikisinin Models/
   ViewModel/State companion dosyaları ile kendi test projeleri
   tamamen silindi; `ALKAROS.slnx`'ten ilgili iki proje girdisi
   kaldırıldı.
6. Yeni HTTP testi: bir masa oluşturuluyor (`CurrentOrderTotal == 0`
   doğrulanıyor), yeni `SeedCurrentOrderWithItemsAsync` yardımcı
   metoduyla bir sipariş + 3 kalem (60 Active, 90 Draft, 500 Cancelled)
   seed ediliyor, hem liste hem detay uç noktasının `150.00` döndürdüğü
   (Cancelled kalemin hariç tutulduğu) doğrulanıyor.

## Out of scope

- Diğer `ToDto` çağrı yerlerine (create/update/status-change/floor-plan/
  merge yanıtları) `CurrentOrderTotal`'ı gerçek değerle doldurmak —
  WaiterPwa bu aksiyonlardan sonra zaten listeyi yeniden çekiyor, ek bir
  sorgu/join gerektirmiyor.
- Denetimin daha önce kapatılan diğer tüm mimari bulguları (Kitchen
  fiziksel yazdırma → V1-RMD-130, Menu → V1-RMD-131, Purchasing/
  Production → V1-RMD-132/133, BuildingBlocks ölü temel kütüphaneleri →
  V1-RMD-134).

## Dependencies

- V1-RMD-129

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Debug`: 0 Uyarı, 0 Hata.
- `node --check src/Clients/WaiterPwa/wwwroot/waiter-app.js`: söz dizimi
  hatası yok.
- `docker compose -f compose.test.yaml run --build --rm test dotnet test
  tests/Host/Experience/Tables/ALKAROS.Host.Experience.Tables.Tests.csproj
  -c Release`: gerçek Postgresql'e karşı **Passed! Failed: 0, Passed: 11,
  Skipped: 0, Total: 11** — yeni test
  `TableListAndDetailReportTheCurrentOrdersRealRunningTotal` dahil, real
  exit code 0.
- `python tools/plan-audit/plan_audit_tool.py validate`: 0 hata, 0
  uyarı.
- `python tools/consistency-audit/consistency_audit.py`: 1 önceden var
  olan, ilgisiz ihlal (değişmedi), yeni ihlal yok.

## Handoff

- None
