# V1-WTR-025 - Kurs yönetimi (sırala / beklet / ateşle)

- Task ID: V1-WTR-025
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

`garson-karsilastirma` karşılaştırma dokümanının son büyük eksiği: kurs
yönetimi. Semih'in talimatıyla ("Devam", ardından tam kurs modeli seçimi,
2026-09-11) — her kaleme kurs numarası, mutfak fişi kurslara bölünür,
"ateşle" ayrı bir eylem.

**Önce mevcut altyapı incelendi:** `Order.AddRound`/`FireRound` zaten bir
"tur" (round) kavramı taşıyor, ama bir tur TÜMÜYLE birlikte ateşleniyor —
gönder ya da bekle, ara durum yok. Gerçek kurs yönetimi bundan farklı:
mutfak TÜM kursları önceden görmeli (hazırlık planlaması için — fiş bazlı,
ekransız bir mutfak sistemi bu yüzden ekstra önemli: sonradan gelecek bir
kursu "gizlemek" yerine fişte GÖRÜNÜR ama "BEKLETİLİYOR" diye işaretli
basılıyor), ama yalnız ateşlenen kurs üzerinde çalışmalı.

**Çözüm — `OrderItem.CourseNumber` (nullable, 1-20) + yeni `KitchenState.Held`:**

- `Order.FireRound` artık bir turdaki TÜM Draft kalemleri birlikte
  aktive ediyor (hesaba TÜMÜYLE işleniyor — sadece 1. kurs değil), ama
  kurs numarası taşıyan kalemlerde yalnız o turun EN DÜŞÜK kurs numarası
  doğrudan `Sent` oluyor; aynı turdaki daha yüksek kurs numaraları
  `Held` oluyor. Kurs numarası olmayan kalemler (mevcut her akış —
  Cashier, NFC, QR, kurs kullanmayan garson siparişleri) hiç
  etkilenmiyor, her zamanki gibi doğrudan `Sent`.
- `Order.FireCourse(courseNumber)` — yeni, ayrı eylem: o kurs numarasının
  `Held` kalemlerini `Sent`'e yükseltir, aynı `(Order, FiredItems)`
  şeklini `FireRound` gibi döner.
- Mutfak fişi (`KitchenTicket`/`KitchenTicketItem`) artık
  `CourseNumber` + `IsHeld` (fiş oluşturma anının anlık görüntüsü)
  taşıyor; `EscPosTicketFormatter` her kalemin kurs başlığını basıyor,
  Held bir kalemin altına "HAZIRLAMAYIN - ATES BEKLENIYOR" satırı
  ekliyor. Ekransız/fiş-bazlı mutfak mimarisine uygun: bütün kurs planı
  TEK fişte baştan basılıyor (hazırlık görünürlüğü), "ateşle" eylemi
  ayrı, küçük bir "fire fişi" ile o kursu çağırıyor —
  `KitchenOrderSubmissionDispatcher`'ın kendi "zaten fişlendi" tekrar
  koruması bilerek YENİDEN KULLANILMADI (bu kalemler zaten ilk fişte var
  — o koruma "ateşle"yi sessizce atlardı); bunun yerine
  `OrderManagementStore.FireCourseAsync` kendi fiş numarası bazlı
  idempotency'sini taşıyor (`KT-{sipariş}-{istasyon}-FIRE-{kurs}`, aynı
  fiş numarasıyla tekrar çağrı no-op).
- Yeni uç nokta: `POST /orders/{orderId}/fire-course`
  (`FireCourseRequestV1(CourseNumber)`), submit-draft ile aynı yetki
  (`orders.send`) — ikisi de "mutfağa gönder", biri yeni tur biri
  bekleyen bir kurs için.
- WaiterPwa: ürün sepetinde opsiyonel "Kurs" seçici (koltuk seçicinin
  yanında, aynı `qty-quick` deseni); sipariş ekranında Held bir kalemin
  altında "Kursu ateşle" butonu.

**Kapsam dışı bırakılan (bilinçli):** KDS (mutfak ekranı) tarafı — bu
kod tabanında hiç yok, mutfak tamamen ESC/POS fiş bazlı
(`KitchenPrinterSimulator`, ekran/canlı senkron `kitchen.live_sync_enabled`
varsayılan kapalı). "Ateşle" bu yüzden ekranda bir durumu değiştirmek
değil, yeni bir fiş bastırmak olarak tasarlandı — mevcut mimariyle
tutarlı.

## Owned surface

- `plan/v1/waiter-pwa/V1-WTR-025-course-management.md` (yeni)
- `database/migrations/V1/V1-WTR-025/105-order-items-course.up.sql`
- `database/migrations/V1/V1-WTR-025/105-order-items-course.down.sql`
  (ikisi de yeni) — `orders.order_items.course_number`,
  `kitchen_state` CHECK'ine `Held` eklendi.
- Sınırlı ek:
  - database/MigrationComposition/order.json (V0-DAT-001 sahipliğinde)
    — yeni "105" pozisyonu.
  - src/Host/Composition/Migrations/MigrationManifest.cs (Host
    sahipliğinde) — `PhaseBMax` "104" → "105".
  - src/Modules/Orders/OrderAggregate/OrderEnums.cs, OrderItem.cs,
    Order.cs, PostgresOrderRepository.cs (Orders sahipliğinde) —
    `KitchenState.Held`, `CourseNumber`, `FireCourse`, kolon taşıma.
  - src/Modules/Orders/ItemExceptions/ItemExceptionHandler.cs (Orders
    sahipliğinde) — comp yolundaki elle kurulan `new OrderItem(...)`'a
    `courseNumber` eklendi (ServingUserId hata sınıfı önlemi).
  - src/Modules/Kitchen/TicketLifecycle/KitchenTicketItem.cs,
    KitchenTicket.cs (Kitchen sahipliğinde) — `CourseNumber`, `IsHeld`.
  - src/Modules/Kitchen/PrintQueue/EscPosTicketFormatter.cs (Kitchen
    sahipliğinde) — kurs başlığı + "BEKLETİLİYOR" satırı basımı.
  - src/Host/Experience/Orders/OrderManagementContracts.cs,
    OrderManagementStore.cs, OrderManagementEndpoints.cs (Host
    sahipliğinde) — `CourseNumber` alanları, `FireCourseAsync`,
    `POST /fire-course`.
  - src/Clients/WaiterPwa/wwwroot/waiter-app.js, waiter-app.css
    (WaiterPwa sahipliğinde) — kurs seçici, "Kursu ateşle" butonu,
    `.is-held` rozet.
  - 21 test .csproj dosyası (her test modülünün kendi sahipliğinde) —
    `105-order-items-course.up.sql` fixture eklendi (V1-WTR-017/022'nin
    aynı 21 projeli desenini tekrar eden schema-değişikliği blast
    radius'u — `orders.order_items` `PostgresOrderRepository.BindItem`
    tarafından koşulsuz bağlanıyor).
  - tests/Modules/Orders/OrderAggregate/OrderDomainTests.cs,
    tests/Host/Experience/Orders/TableDraft/OrderManagementTableDraftHttpTests.cs
    (ilgili test modüllerinin sahipliğinde) — yeni testler.
  - tests/Host/MigrationComposition/Manifest/ManifestTests.cs (Host
    test sahipliğinde) — beklenen migrasyon sayısı/listesi 103→104
    (yeni pozisyon eklendiği için güncellendi).

## Out of scope

- KDS (mutfak ekranı) tarafı — kod tabanında hiç yok, ayrı bir görev.
- Kurs sıralamasının zorunlu ardışıklığı: `FireCourse` yalnız o kurs
  numarasının Held kalemi olup olmadığını kontrol ediyor, kurs 3'ü kurs
  2'den önce ateşlemeyi engellemiyor — gerçek bir restoranda planın
  değişebileceği (bir kurs atlanabilir) varsayılıyor, ayrı bir iş
  kuralı istenirse sonraki görev.
- Çoklu istasyon yönlendirmesi: `FireCourseAsync` submit-draft'ın kendi
  belgelenmiş "router verilmezse tek varsayılan istasyon" davranışını
  aynen taşıyor, ayrı per-item routing eklemedi.

## Dependencies

- V1-ORD-006
- V1-WTR-022

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Debug` (tüm çözüm) → 0 uyarı, 0 hata.
- `ALKAROS_TEST_PG_PORT=55432 ALKAROS_TEST_PG_PASSWORD=postgres
  ALKAROS_KITCHEN_STATION_ID=test-station dotnet test`:
  - `tests/Modules/Orders/OrderAggregate` → 125/125 (5 yeni test: çoklu
    kurslu bir turun ateşlenmesi yalnız en düşük kursu Sent yapar
    gerisini Held bırakır; kurs numarası olmayan kalem etkilenmez;
    `FireCourse` yalnız o kursu Sent'e çeker; Held kalemi olmayan bir
    kursu ateşlemek hata verir; aynı kursu iki kez ateşlemek ikincide
    hata verir).
  - `tests/Host/Experience/Orders/TableDraft` → 66/66 (4 yeni test:
    çok kurslu bir taslağı göndermek geç kursları bekletir + tek fiş
    basar; kursu ateşlemek o kursu Sent'e çeker + ikinci bir fiş basar;
    hiç Held kalemi olmayan bir kursu ateşlemek 409 döner; aynı kursu
    iki kez ateşlemek ikincide 409 döner, ikinci fiş basılmaz).
  - `tests/Modules/Kitchen/TicketLifecycle` → 20/20, `tests/Modules/Kitchen/PrintQueue`
    → 27/27 (regresyon — fiş oluşturma ve ESC/POS biçimlendirme).
  - `tests/Host/Experience/Orders/{Comp,VoidSent,Void,Confirmation}`,
    `tests/Modules/Orders/{ItemExceptions,SubmitOrder}`,
    `tests/Host/Experience/QrOrdering` → hepsi yeşil (regresyon —
    `KitchenState.Held` eklenmesinin ServingUserId sınıfı hatası
    yaratmadığının kanıtı).
  - `tests/Host/MigrationComposition` → 135/135 (tam paket — izole
    filtre değil; yeni migrasyon pozisyonunun `MigrationManifest`
    `PhaseBMax`'ı ve `order.json`'daki "105" girdisi olmadan tüm Host
    kompozisyonunun `StartupFailed` ile çöktüğü bu adımda bulundu ve
    düzeltildi).
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → bu görevin
  değiştirdiği dosyalarda 0 ihlal. Kalan tek ihlal,
  `src/Modules/Inventory/ManualAdjustments/InventoryAdjustmentService.cs:96`,
  bu görevden önce vardı ve sahip olunan yüzeyin dışında.

## Handoff

- None
