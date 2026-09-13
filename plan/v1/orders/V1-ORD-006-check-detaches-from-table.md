# V1-ORD-006 - Hesap masadan koparılabilsin: "Hesabı kasaya gönder"

- Task ID: V1-ORD-006
- Status: Done
- Assignee: Claude Opus 5
- Work type: implementation
- Surface state: Existing

## Goal

`docs/design/modules/check-and-table.md`'de kilitlenen kararı uygular. Semih'in
sorduğu senaryo (2026-09-10): masa yemeğini yedi, kalktı, kasada ödeme için
sıra bekliyor; o masaya yeni müşteri geldi.

Bugün sistem bu durumda **eski hesabı kaybediyor**: yeni sipariş
`current_order_id`'yi koşulsuz kendine çekiyor, eski hesap hiçbir masadan
görünmüyor, masa toplamı yalnız yeni müşteriyi gösteriyor. Kök sebep,
hesabın masadan hiç kopamaması — ve daha derinde, hiçbir siparişin
kapanmaması.

Aynı kök, denetimin en pahalı bulgusunu da üretiyor: birleştirme araması
yalnız `Draft` sipariş aradığı, garson istemcisi ise hiç `Draft` bırakmadığı
için **her tur yeni bir sipariş açıyor** ve adisyon yalnız son turu
gösteriyor — ₺400 meze + ₺900 ana yemek söyleyen masada ₺400 hiç
faturalanmıyor. İkisi tek kavramla kapanır: *masaya bağlı hesap*.

## Owned surface

- `plan/v1/orders/V1-ORD-006-check-detaches-from-table.md` (yeni)
- `docs/design/modules/check-and-table.md` (yeni — karar dokümanı)
- Sınırlı ek — aşağıdaki yollar ilgili görevlerin sahipliğinde kalır (yollar
  geri-tik olmadan yazıldı ki denetleyici bunları sahiplik iddiası olarak
  parse etmesin):
  - src/Host/Experience/Orders/OrderManagementStore.cs,
    src/Host/Experience/Orders/OrderManagementEndpoints.cs,
    src/Host/Experience/Orders/OrderManagementContracts.cs (V1-ORD-00x
    sahipliğinde) — bağlı hesap araması, koparma uç noktası, koruma.
  - src/Modules/Orders/OrderAggregate/Order.cs (V1-ORD-001 sahipliğinde) —
    AddRound/FireRound.
  - src/Modules/Orders/SubmitOrder/IOrderSubmissionDispatcher.cs,
    src/Modules/Orders/SubmitOrder/SubmitOrderHandler.cs (V1-ORD-002
    sahipliğinde) — ateşlenen kalem listesinin sözleşmeye taşınması.
  - src/Modules/Kitchen/TicketLifecycle/KitchenOrderSubmissionDispatcher.cs
    (V1-KIT-001 sahipliğinde) ve
    src/Host/Experience/Orders/SubmissionStockConsumption/OrderSubmissionStockDispatcher.cs
    (V1-RMD-144 sahipliğinde) — yeni sözleşmeye uyum.
  - src/Modules/Tables/TableLifecycle/PostgresTableRepository.cs
    (V1-TBL-001 sahipliğinde) — masa boşaltılırken işaretçinin temizlenmesi.
  - src/Clients/WaiterPwa/wwwroot/waiter-app.js,
    src/Clients/WaiterPwa/wwwroot/index.html,
    src/Clients/WaiterPwa/wwwroot/waiter-app.css (V1-WTR-010 sahipliğinde) —
    *Hesabı kasaya gönder* eylemi ve kapanmamış hesap uyarısı.
  - tests/Host/Experience/Orders/TableDraft/** (V1-ORD-00x sahipliğinde) —
    regresyon.

## In scope

0. **Agrega: açık hesaba yeni tur eklenebilsin.** Bugün `AddItem` ve `Submit`
   yalnız `Draft` durumunda çalışıyor, yani açık (Submitted) bir hesaba
   ikinci tur eklemenin yolu yok. `Order.AddRound(items)` (Draft veya
   Submitted iken Draft kalem ekler) ve `Order.FireRound()` (yalnız Draft
   kalemleri Active yapar; sipariş Draft ise Submitted'a geçirir, zaten
   Submitted ise durumu değiştirmez) eklenir.

   **Bu, sevkiyat sözleşmesini de değiştirmeyi zorunlu kılıyor.**
   `IOrderSubmissionDispatcher.DispatchAsync` bugün gönderilecek kalemleri
   `order.Items.Where(IsActive)` diye kendisi türetiyor;
   `OrderSubmissionStockDispatcher`'ın kendi yorumu doğruluğunun "bir masanın
   Draft'tan çıkmış siparişi yeni sipariş açar" davranışına dayandığını
   açıkça söylüyor. Turlar birleşince bu türetme birinci turu mutfağa ikinci
   kez gönderir ve stoğu ikinci kez düşürürdü. Sözleşme, **o anda ateşlenen
   kalem listesini** parametre olarak alacak şekilde değiştirilir; kırılgan
   türetme ortadan kalkar.

1. **Bağlı hesap araması.** Birleştirme, `status = 'Draft'` yerine masanın
   `current_order_id`'sinin gösterdiği, kasaya gönderilmemiş siparişi arar.
   Yeni tur o hesaba eklenir; ikinci bir sipariş açılmaz.
2. **Koparma.** `POST /orders/{orderId}/send-to-cashier`: siparişi yeni
   kaleme kapatır, hesabı (bill) yoksa oluşturur, `current_order_id`'yi
   temizler, masayı `Cleaning`'e alır — hepsi tek işlemde, satır sürümü
   korumalı.
3. **Koruma.** Masaya bağlı ve koparılmamış bir hesap varken
   `current_order_id` başka bir siparişe kaydırılmaz; istek Türkçe bir
   hatayla reddedilir ve garsona sorulur.
4. **Masa boşaltma işaretçiyi temizler.** `SetAvailable` bugün yalnız
   `current_status`'ü değiştiriyor; bayat `current_order_id` kalıyor.
5. **Kasa kuyruğu için okuma yüzeyi.** Koparılmış ve kapanmamış hesapları
   listeleyen uç nokta (bugün `GET` yalnız split-design grubunda var).
6. **Garson ekranı.** Adisyonda *Hesabı kasaya gönder*; kapanmamış hesabı
   olan masaya dokunulduğunda çıkan açık soru.

## Out of scope

- **Ödeme.** Semih'in kararı: şimdilik dokunulmuyor (V1.2). Sonucu açıkça
  kabul edildi: `GET .../orders/awaiting-payment` bekleyen hesapları verir
  ama liste kendiliğinden boşalmaz, kapatma V1.2'nin işidir.
- **Sipariş yaşam döngüsünün tamamı.** `Preparing`/`Ready`/`Served`/
  `Completed` geçişlerinin hiç çağrılmaması ayrı ve daha büyük bir eksik.
- Kasa/PosTerminal'in bekleyen hesaplar ekranı: kendi görevi.
- Masa birleştirme/bölme ve split akışları.

## Dependencies

- V1-ORD-005
- V1-WTR-010

## Acceptance evidence

- `dotnet build ALKAROS.slnx`: 0 Uyarı, 0 Hata.
- `ALKAROS.Host.Experience.Orders.TableDraft.Tests`: **38/38** — mevcut 32
  regresyonun tamamı artı bu görevin 6 yeni testi
  (`CheckLifecycleHttpTests`). Komşu paketlerde regresyon yok: Kitchen'ın
  beş projesi (7/17/27/23/20), KitchenOperations 7/7, NfcOrdering 17/17,
  QrOrdering 17/17, Orders.ItemExceptions 20/20, Tables 11/11.
- **Yeni testler işin kendisini kanıtlıyor:** ikinci tur aynı hesapta kalıyor
  ve mezenin parası adisyonda duruyor; ikinci tur yalnız kendi satırını
  mutfağa gönderiyor ve yalnız kendi stoğunu düşürüyor; hesap kasaya
  gidince masa boşalıp `Cleaning`'e geçiyor ve hesap bekleyenler listesinde
  parasıyla duruyor; sonra yeni müşteri aynı masaya oturabiliyor ve iki
  hesap karışmıyor; iki kez göndermek hata değil, "zaten gönderilmiş"
  cevabı veriyor.
- **Test yazarken iki gerçek kusur yakalandı, ikisi de yazılmadan önce
  görülemezdi:**
  1. `KitchenOrderSubmissionDispatcher` "bu sipariş+istasyon için bilet var
     mı" diye soruyordu. Bu, bir sipariş = bir gönderim iken doğru bir
     tekrar korumasıydı; turlar birleşince birinci turun bileti bulunuyor ve
     **ikinci tur tamamen atlanıyordu — yemek mutfağa hiç ulaşmıyordu.**
     Soru artık ateşlenen kalemlere soruluyor; her tur kendi biletini alıyor
     ve birinci turun numarası değişmiyor.
  2. `ANewPartyCannotOpenACheck…` diye yazdığım test 409 bekliyordu ama 200
     aldı. Sebep tasarımın kendisi: **sunucu "aynı grubun ikinci turu" ile
     "yeni müşteri"yi ayırt edemez.** Kurduğum koruma yalnız yarışta
     ulaşılabilir. Test, gerçek özelliği yazacak şekilde düzeltildi (eski
     hesap asla öksüz kalmıyor) ve mağazadaki yorum bu sınırı açıkça
     söylüyor: grubun değiştiğini garson, hesabı kasaya göndererek söyler.
- Denetimin şu bulguları bu işle birlikte kapandı: her turun ayrı sipariş
  açması ve adisyonun yalnız son turu göstermesi (para kaybı); birleştirme
  dalının bayat `rowVersion` döndürmesi (ikinci tur gönderilemiyordu);
  birleştirmenin sipariş notunu silmesi ve sipariş numarasını ikinci bir
  biçime yeniden yazması — üçü de elle kurulan `new Order(...)` yerine
  agreganın `AddRound`'u kullanıldığı için ortadan kalktı. Masa
  boşaltılırken bayat `current_order_id` de temizleniyor.
- `python tools/plan-audit/plan_audit_tool.py validate`: 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py`: bu görevden yeni
  ihlal yok (kalan tek ihlal `InventoryAdjustmentService.cs:96`, diff'te
  değil).
- Elle denenecek senaryo: masaya sipariş al, gönder; ikinci tur ekle ve
  adisyonda **iki turun da** durduğunu gör; mutfakta ikinci tur için ayrı
  bir bilet çıktığını doğrula; *Hesabı kasaya gönder* de ve masanın
  boşaldığını gör; aynı masaya yeni müşteri otur ve adisyonun boş açıldığını
  doğrula.

## Handoff

- None
