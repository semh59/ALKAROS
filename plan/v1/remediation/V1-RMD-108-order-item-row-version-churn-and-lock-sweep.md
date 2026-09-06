# V1-RMD-108 - Order item row_version churn fix and cross-module lock sweep

- Task ID: V1-RMD-108
- Status: Done
- Assignee: claude-session-01GGsiy81vQBnzkRKVfPsjMC
- Work type: implementation
- Surface state: Existing

## Goal

Semih onayıyla (önceki oturumda önerilen madde listesinden devam, "devam",
2026-09-06), iki madde daha kapatıldı:

1. `PostgresOrderRepository.SaveAsync`, `order.Items` içindeki bilinen her
   kalem için, hiçbir alanı değişmemiş olsa bile `UpdateItemAsync`
   çağırıyordu — Kitchen ticket'ta daha önce bir kez düzeltilen sahte-
   concurrency kusurunun aynısı. Artık bir kalemin karşılaştırılabilir
   alanları (miktar, tutarlar, durum, mutfak durumu, not) değişmediyse
   UPDATE hiç çalışmıyor, `row_version` gereksiz artmıyor.
2. `V1-RMD-106`/`107`'de bulunan self-deadlock sınıfının (bir kilit tutan
   bağlantıdan ayrı bir bağlantıyla FK-referanslı yazma) başka yerlerde de
   olup olmadığı tarandı: repository/dosya bazında her `FOR UPDATE`
   kullanımı (`SubmitOrderHandler`, `DualScreenStore.Orders.cs`/`.Display.cs`,
   `PostgresSplitDesignRepository`) tek tek okunup doğrulandı. Hepsi zaten
   kilidi tutan `connection`/`transaction`'ı sonraki her yazımına açıkça
   geçiriyor — aynı kusur başka hiçbir yerde bulunmadı. `V1-RMD-106`/`107`'nin
   düzelttiği iki yer (Orders table-draft, Billing indirim), bu oturumda
   yeni eklenen ve mevcut disiplini henüz almamış kod olduğu için istisnaydı.

## Owned surface

- `plan/v1/remediation/V1-RMD-108-order-item-row-version-churn-and-lock-sweep.md`
- Sınırlı ek — aşağıdaki yol ilgili görevin sahipliğinde kalır (yol geri-tik
  olmadan yazıldı ki denetleyici sahiplik iddiası olarak parse etmesin):
  - src/Modules/Orders/OrderAggregate/PostgresOrderRepository.cs ve
    tests/Modules/Orders/OrderAggregate/PostgresOrderTests.cs (V1-ORD-001
    sahipliğinde) — `SaveAsync` artık değişmeyen kalemler için UPDATE
    atlıyor; regresyon testi eklendi.

## In scope

- `PostgresOrderRepository.SaveAsync`: `ReadItemIdsAsync` (yalnız id)
  `ReadItemSnapshotsAsync`'e (id + karşılaştırılabilir alanlar) genişletildi;
  bir kalem yalnızca `ItemSnapshot.Matches` false dönerse güncelleniyor.
- Regresyon testi: bir siparişe ikinci bir kalem eklenip kaydedildiğinde,
  ilk kalemin `row_version`'ı değişmiyor. Revert-and-confirm ile doğrulandı:
  düzeltme geri alındığında test beklenen şekilde (1 yerine 2) başarısız
  oluyor.
- Cross-module kilit taraması: `grep -rl "FOR UPDATE"` ile bulunan tüm V1
  kapsamındaki Host/Experience orkestrasyon kodu (`SubmitOrderHandler.cs`,
  `DualScreenStore.Orders.cs`, `DualScreenStore.Display.cs`,
  `PostgresSplitDesignRepository.cs`) okunup her yazımın kilidi tutan aynı
  `connection`/`transaction`'ı kullandığı doğrulandı.

## Out of scope

- V1.1 (Inventory/Recipes/Menu/Production/Purchasing) modüllerindeki
  `FOR UPDATE` kullanımları — ayrı, eşzamanlı ilerleyen bir oturumun
  kapsamında, dokunulmadı.
- Cashier/WaiterPwa JS test altyapısı, garson-masa servis atama modeli —
  ayrı görevlere bırakıldı (önceki oturumda Semih'e bildirildi).

## Dependencies

- V1-RMD-107

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Debug`: 0 uyarı / 0 hata.
- `dotnet test tests/Modules/Orders/OrderAggregate/...`: 103/103 (yeni
  `SaveAsyncDoesNotBumpAnItemsRowVersionWhenNothingAboutItChanged` dahil).
- `dotnet test` ilgili Orders/Billing HTTP süitleri (TableDraft 4/4,
  Comp 7/7, Void 5/5, VoidSent 8/8): regresyonsuz.
- Revert-and-confirm: düzeltme geçici olarak geri alınıp yeni test
  çalıştırıldı, beklenen mesajla (Expected: 1, Actual: 2) başarısız oldu;
  düzeltme geri konulup tam süit yeniden yeşil.
- `python tools/consistency-audit/consistency_audit.py`: bu görevin
  değiştirdiği tek dosyada (`PostgresOrderRepository.cs`) sıfır ihlal.
  Depoda toplam 7 ihlal var, hepsi `src/Modules/Inventory/**` ve
  `src/Clients/Cashier/MenuRecipeAdmin|Production/**` altında — bu görevin
  sahiplemediği, eşzamanlı ilerleyen başka bir oturuma (V1.1) ait dosyalar;
  dokunulmadı.
- `python tools/plan-audit/plan_audit_tool.py validate` ve
  `validate-coverage`: sıfır hata.

## Handoff

- V1-GOV-093
