# V1-RMD-154 - İptal: stok iadesi, mutfak duvarı ve masa sahipliği

- Task ID: V1-RMD-154
- Status: Done
- Assignee: Claude Opus 5
- Work type: remediation
- Surface state: Existing

## Goal

2026-09-10 denetiminin sunucu tarafındaki üç bulgusu. Üçü de kalem iptalinde
buluşuyor ve ikisi aynı kökten geliyor.

**Kök: hiçbir kalem "mutfağa gönderildi" olarak işaretlenmiyor.**
`Order.Submit()` yalnız `item.Activate()` çağırıyor; `KitchenState`'i
ilerleten tek şey KDS canlı senkronu ve o da varsayılan kapalı
(`kitchen.live_sync_enabled`). Yani sipariş mutfağa gidiyor, bilet basılıyor,
stok düşüyor — ama kalem hâlâ `NotSent` görünüyor. Bunun iki sonucu var:

1. `VoidItemAsync`'in "zaten mutfağa gitmiş" duvarı hiç devreye girmiyor.
   Garson, yenmiş dört yemeği `orders.create` yetkisiyle iptal edebiliyor:
   `bills.void` onay akışı atlanıyor, mutfak bileti iptal edilmiyor.
2. Submit'te düşen stok iade edilmiyor. İadeyi yapan tek kod
   (`SentItemVoidStore.RestoreStockForVoidedItemAsync`) yalnız `Sent`
   durumunda çalışıyor, dolayısıyla erişilemiyor. Her iptal depo sayısını
   kalıcı olarak aşağı kaydırıyor.

Üçüncü bulgu bağımsız: `ItemExceptionHandler` void ve comp'ta agregayı 21
konumsal argümanla yeniden kuruyor ve 22. parametre olan `servingUserId`
varsayılan `null`'a düşüyor — `UpdateOrderAsync` bunu yazıyor. Her void/comp
masanın garsonunu siliyor; masa sahipliği kontrolü (own-check) devre dışı
kalıyor ve `transfer-server` o siparişi devredemiyor.

## Owned surface

- `plan/v1/remediation/V1-RMD-154-void-restores-stock-and-respects-the-kitchen.md` (yeni)
- Sınırlı ek — aşağıdaki yollar ilgili görevlerin sahipliğinde kalır (yollar
  geri-tik olmadan yazıldı ki denetleyici bunları sahiplik iddiası olarak
  parse etmesin):
  - src/Modules/Orders/OrderAggregate/Order.cs (V1-ORD-001 sahipliğinde) —
    ateşlenen kalem mutfağa gönderilmiş sayılır.
  - src/Modules/Orders/ItemExceptions/ItemExceptionHandler.cs (V1-ORD-003
    sahipliğinde) — servingUserId korunur, ateşlenmemiş kalem iptal
    edilebilir.
  - src/Clients/WaiterPwa/wwwroot/waiter-app.js (V1-WTR-010 sahipliğinde) —
    İptal düğmesi yalnız gerçekten iptal edilebilir satırda.
  - tests/Modules/Orders/**, tests/Host/Experience/Orders/** (ilgili
    görevlerin sahipliğinde) — regresyon.

## In scope

1. **Ateşleme kalemi `Sent` yapar.** `Order.FireRound` aktive ettiği
   kalemleri aynı anda `KitchenState.Sent`'e alır. Mutfak bileti aynı
   işlemde yazıldığı için ikisi atomik; bilet yazılamazsa durum da geri
   alınır.
2. **Duvar çalışır hâle gelir.** Böylece `/void` (yalnız oturum yetkisi)
   ateşlenmiş kalemi reddeder ve yenmiş yemek yalnız `/void-sent` üzerinden,
   yani `bills.void` onay akışıyla iptal edilebilir — o yol bileti de iptal
   ediyor, stoğu da iade ediyor.
3. **Stok açığı yapı gereği kapanır.** Stok ateşlemede düşüyor; ateşlenmemiş
   (Draft) kalem hiç stok tüketmemiş oluyor, dolayısıyla iptalinde iade
   edilecek bir şey yok. Ateşlenmiş kalem ise artık `/void-sent`'e gidiyor ve
   orada zaten iade ediliyor.
4. **Ateşlenmemiş kalem iptal edilebilir.** Bugün `/void` yalnız `Active`
   kalem kabul ediyor; açık bir hesaba eklenmiş ama henüz gönderilmemiş
   (Draft) kalemi iptal etmek imkânsız ve hata 409 "başka bir işlem
   değiştirdi" diye görünüyor (denetim M2). Draft kalem de kabul edilir.
5. **`servingUserId` korunur** void ve comp yeniden kurulumlarında.
6. **İstemci:** İptal düğmesi sunucunun söylediği duruma göre gösterilir.

## Out of scope

- `Preparing`/`Ready`/`Served`/`Completed` geçişlerinin hiç çağrılmaması:
  ayrı ve daha büyük bir eksik (bkz. `docs/design/modules/check-and-table.md`).
- Denetimin diğer sunucu bulguları (`PostgresException`'ın 503'e eşlenmesi,
  eklenti seçim kurallarının sunucuda zorlanmaması, kasa sahte masası).
- Veritabanı kısıt eksikleri (negatif miktar, eksik foreign key'ler).

## Dependencies

- V1-ORD-006

## Acceptance evidence

- `dotnet build ALKAROS.slnx`: 0 Uyarı, 0 Hata.
- Etkilenen bütün paketler yeşil: Orders (ItemExceptions 20, OrderAggregate
  120, SubmitOrder 16), Host Orders (Comp 9, Confirmation 19, **TableDraft
  40**, Void 5, VoidSent 13), Kitchen'ın beş projesi (7/17/27/23/20),
  KitchenOperations 7, NFC 17, QR 17, Billing 13, Tables 11, MigrationComposition 134.
- **Düzeltmenin kendisini kanıtlayan iki yeni uçtan uca test**
  (`CheckLifecycleHttpTests`):
  - *Gerçekten mutfağa gitmiş kalem ucuz yoldan iptal edilemez.* Gerçek
    gönderim yolundan geçen kalem artık `KitchenState: "Sent"` bildiriyor,
    `/void` 409 dönüyor ve stok iade edilmiyor (yani kalem hâlâ tüketilmiş
    sayılıyor) — yenmiş yemek `bills.void` onay akışına gitmek zorunda.
  - *Gönderilmemiş kalem iptal edilebilir ve hiç stok tüketmemiş.* Yalnız
    draft oluşturulup gönderilmeyen kalem 200 ile iptal ediliyor, stok 50
    kalıyor. Bu daha önce imkânsızdı ve 409 "başka bir işlem değiştirdi"
    diye görünüyordu (denetim M2).
- **Mevcut iki test bilerek değiştirildi, gevşetilmedi:**
  1. `ASubmittedItemReportsItsStatusKitchenStateAndCreationTime` `"NotSent"`
     bekliyordu. Bu, sözleşme değil kusurun kendisiydi: kalem mutfağa
     gönderilmiş ve bileti basılmışken "gönderilmedi" diyordu. Beklenen
     değer `"Sent"` oldu, gerekçesi testin içine yazıldı.
  2. `HandleAsyncInvalidTransitionWhenOrderNotInDraftState…` "Submitted
     sipariş yeniden gönderilemez" diyordu; V1-ORD-006 bu kuralı bilerek
     değiştirdi (açık hesap ikinci turu kabul eder). Test, yerini alan
     kuralı yazacak şekilde yeniden adlandırıldı ve düzeltildi: ateşlenecek
     kalemi kalmamış bir gönderim hâlâ hata veriyor.
- `ALKAROS.Orders.OrderAggregate.Tests` süpürmede bir kez 119/120 düştü;
  arka arkaya üç kez tekrar çalıştırıldı, üçünde de 120/120. Sebep aynı
  komutta çalışan solution build'iyle `--no-build` testinin yarışmasıydı,
  kodla ilgisi yok — "dalgalanma" diye geçilmedi, doğrulandı.
- `node --check waiter-app.js`: temiz.
- `python tools/plan-audit/plan_audit_tool.py validate`: 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py`: denetim bu turda bir
  kez haklı çıktı — kendi yorumumda Türkçe alıntı bırakmıştım, İngilizceye
  çevrildi. Kalan tek ihlal `InventoryAdjustmentService.cs:96`, diff'te değil.

## Handoff

- None
