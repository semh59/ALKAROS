# V1-RMD-158 - Backend bölümünün kalan altı bulgusu

- Task ID: V1-RMD-158
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: remediation
- Surface state: Existing

## Goal

`docs/engineering/garson-audit-2026-09-10.md`'nin Backend bölümündeki 8
bulgudan ikisi zaten kapandı (negatif `price_delta`, V1-RMD-156'nın bir
parçası olarak `Modifier.cs`'e eklendi ama henüz commit edilmemişti; kasanın
sahte masası V1-RMD-157'de çözüldü). Bu görev kalan altısını kapatır:

1. **Negatif `price_delta` eklentisi** — katalog kabul ediyordu, sipariş
   agregası (`OrderItemModifier`) reddediyordu; ürün asla sipariş
   edilemiyordu. `Modifier.cs`'in kurucusuna aynı kural eklendi
   (V1-RMD-156 çalışması sırasında yazıldı, bu görevle commit ediliyor).
2. **Stok hareketinin aktörü kasa satışında sıfır UUID** —
   `OrderSubmissionStockDispatcher.DispatchAsync`, `order.ServingUserId ??
   Guid.Empty` ile sessizce "kimse" anlamına gelen boş UUID'ye düşüyordu.
   `ServingUserId`, `OrderManagementStore.CreateOrUpdateTableDraftAsync`
   tarafından her zaman gerçek, kimliği doğrulanmış personel ile
   dolduruluyor — null olması bir değişmezin bozulduğu anlamına gelir,
   sessizce yutulacak bir durum değil. Artık `InvalidOperationException`
   fırlatıyor.
3. **Kalem sıralaması eşitlikte rastgele** —
   `PostgresOrderRepository.ReadItemsAsync`, `ORDER BY created_at` kullanıyordu;
   aynı turdaki tüm kalemler aynı `now` damgasını paylaştığı için (
   `OrderManagementStore` bütün turu tek bir `DateTimeOffset.UtcNow` ile
   kuruyor) eşitlikler sık, Postgres'in eşitlik-bozma sırası belirsiz.
   `DualScreenStore.Display.cs`'in aynı tablo için zaten kullandığı
   `, order_item_id` ikincil sıralamasıyla aynı hizaya getirildi.
4. **Mutfak geçişinde commit sonrası hata yanlışlıkla eşzamanlılık
   çakışması diye raporlanıyor** — `KitchenOperationsStore`,
   `InvalidKitchenTransitionException`'ı (bir durum makinesi ihlali, asla
   tekrar denemeyle düzelmez) yakalayıp `KitchenOperationsConcurrencyException`
   olarak yeniden fırlatıyordu — istemciye "başkası değiştirdi, tekrar dene"
   diyordu, oysa gerçek sorun geçişin baştan geçersiz olmasıydı. Endpoint
   filtresinde zaten `InvalidKitchenTransitionException` için doğru,
   ayrı bir `DOMAIN_CONFLICT` eşlemesi vardı — bu yakalama onu hiç
   göremeden maskeliyordu. Yakalama bloğu kaldırıldı, istisna doğal
   olarak doğru eşlemeye ulaşıyor.
5. **`OrderMath.RoundQuantity` ölü** — hiçbir yerden çağrılmıyordu.
   Bulgu 6 ile aynı kökten çıktı: silinecek ölü kod değil, hiç
   bağlanmamış bir düzeltmeydi (bkz. madde 6).
6. **3 ondalıktan fazla miktar her kayıtta gereksiz sürüm artırıyor** —
   `OrderItem` kurucusu miktarı olduğu gibi saklıyordu; Postgres
   `NUMERIC(18,3)` sütununa yazarken zaten yuvarlıyor, ama bellekteki
   değer yuvarlanmamış kalıyordu. `PostgresOrderRepository.ItemSnapshot.Matches`
   düz `decimal ==` karşılaştırması yaptığından, 3 ondalıktan ince bir
   miktar her kayıtta "değişti" gibi görünüp `row_version`'ı gereksiz
   artırıyordu. Kurucu artık `OrderMath.RoundQuantity`'yi kullanıyor —
   hem bu kayıt uyuşmazlığını gideriyor hem de madde 5'teki "ölü kod"u
   gerçek işlevine kavuşturuyor (silmek yerine bağlamak, çünkü kod zaten
   tam olarak bu iş için, `RoundCurrency`'nin hemen yanına yazılmıştı).
   Pozitiflik kontrolü, yuvarlanmış değer üzerinden yapılacak şekilde
   taşındı (3 ondalıktan ince ama pozitif bir giriş artık sıfıra
   yuvarlanıp sessizce geçmek yerine reddediliyor).

**Ayrıca, işi izlerken bulundu, denetimde yoktu:**
`PendingOrderConfirmationStore.AcceptAsync`'in doc yorumu "Cashier/Waiter/
NFC age-restricted/QR'ın hepsi Accepted'a yalnızca bu metottan ulaşır"
diyordu. Yanlış: `OrderSubmissionStockDispatcher`'ın kendi yorumu zaten
açıkça söylüyor — bir Cashier/Waiter siparişi PendingConfirmation'a hiç
girmez, Draft'tan doğrudan Submitted'a geçer ve stoğu orada tüketir. Bu
metoda yalnızca NFC (yaş kontrolü) ve QR kanalları ulaşır. Yorum
düzeltildi.

## Owned surface

- `plan/v1/remediation/V1-RMD-158-backend-audit-remainder.md` (yeni)
- Sınırlı ek (dosyaların tamamı başka görevlere ait; her biri tek, dar bir
  değişiklik):
  - src/Modules/Catalog/ProductCatalog/Modifier.cs (V1-CAT-001
    sahipliğinde) — kurucuya negatif price_delta reddi.
  - src/Host/Experience/Orders/SubmissionStockConsumption/OrderSubmissionStockDispatcher.cs
    (V1-RMD-144 sahipliğinde) — boş UUID aktör düşüşü yerine fırlatma.
  - src/Modules/Orders/OrderAggregate/PostgresOrderRepository.cs
    (V1-RMD-064 sahipliğinde) — kalem sorgusuna ikincil sıralama.
  - src/Host/Experience/KitchenOperations/KitchenOperationsStore.cs
    (V1-RMD-082 sahipliğinde) — yanlış istisna eşlemesinin kaldırılması.
  - src/Modules/Orders/OrderAggregate/OrderItem.cs (V1-RMD-064
    sahipliğinde) — miktarın kurucuda yuvarlanması.
  - src/Host/Experience/Orders/PendingOrderConfirmation/PendingOrderConfirmationStore.cs
    (V1-RMD-137 sahipliğinde) — yalnızca yanlış doc yorumunun düzeltilmesi.

## Out of scope

- Mimari sınırlar bölümü (4 bulgu) — ayrı bir görev.
- API uç noktaları (11) ve Frontend (30) bölümleri — ayrı görevler.

## Dependencies

- V1-RMD-156
- V1-RMD-157

## Acceptance evidence

- `dotnet build src/Host/ALKAROS.Host.csproj -c Debug` → 0 uyarı, 0 hata.
- `ALKAROS_TEST_PG_PORT=55432 ALKAROS_TEST_PG_PASSWORD=postgres dotnet test`
  gerçek test Postgres'ine karşı, bu değişikliklerin dokunduğu her
  yüzeyde:
  - `tests/Modules/Orders/OrderAggregate` — 120/120 yeşil (miktar
    yuvarlama ve pozitiflik kontrolü dahil).
  - `tests/Modules/Orders/SubmitOrder` — 16/16 yeşil.
  - `tests/Host/Experience/KitchenOperations` — 7/7 yeşil (geçiş hatası
    eşlemesi değişikliği dahil).
  - `tests/Modules/Catalog/ProductCatalog` — 81/81 yeşil (negatif
    `price_delta` reddi dahil).
  - `tests/Host/Experience/Orders/{Comp,Confirmation,TableDraft,Void,VoidSent}`
    — toplam 88/88 yeşil (aktör-boş-UUID değişikliği ve `ServingUserId`
    akışı, gerçek HTTP uçları üzerinden).
  - `tests/Host/Experience/QrOrdering` — 17/17 yeşil.
  - `tests/Host/Experience/NfcOrdering` — 16/17; tek başarısızlık
    (`ConcurrentIdenticalFirstSubmissionsResolveToTheSameOrder`) `git
    stash` ile bu görevin dokunduğu 5 dosya geri alınıp aynı test
    tekrar çalıştırılarak **taban çizgide de aynı şekilde
    başarısız olduğu** doğrulandı (gerçek eşzamanlı istek altında ara
    sıra 503 — önceden var olan bir yarış durumu flake'i, bu görevle
    ilgisiz). Değişiklikler geri yüklendi.
  - `tests/Host/MigrationComposition` (bütün Host projesi) — 134/134
    yeşil.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → bu görevin
  değiştirdiği dosyalarda 0 ihlal. (Kalan tek ihlal,
  `src/Modules/Inventory/ManualAdjustments/InventoryAdjustmentService.cs:96`,
  bu görevden önce vardı ve sahip olunan yüzeyin dışında; ayrı bir görev
  için not edildi.)

## Handoff

- None
