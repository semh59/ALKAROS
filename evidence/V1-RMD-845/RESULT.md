# V1-RMD-845 sonuç

Birden çok siparişten toplanan hesap `order_id` taşımıyor; hesabın siparişle bağı artık tek tanımla (kalem yolu) çözülüyor: `BillOrderLink.Matches` (Billing, Host kullanır) ve `TableBillOrderLink.Matches` (Tables modülü, Billing'e referans veremediği için aynı tanım). Bağ: `bills.order_id = sipariş` VEYA hesabın kalemlerinden biri o siparişin kalemi.

## Çözülen tüketiciler (her biri gerçek Postgres testiyle, mutasyonla doğrulandı)
- Billing deposu: `GetByOrderIdAsync` (from-order, kalem koruması, kalem iptal/ikram, bekleyen onay, müşteri ekranı ödeme ilerlemesi bunu kullanır), `CancelActiveBillsForOrderAsync`, `CancelOpenBillsOfOrderAsync`.
- `OrderSettlementService`: ödenmiş birleşik hesap bütün kaynak siparişleri tamamlar (her biri için masa bırakılır).
- `BillClosureSweeper` onarım sorgusu, `OrderBacklogAdministration` (sayım ve toplu kapatma), `OrderCleanupStore` ("askıda sipariş": açık birleşik hesabı olan sipariş iptal edilmez), `BillMoneyGuard` (para denetimi ve kilit), `CashierHandoffStore` (geri alma para denetimi, kuyruk satırı), `ShiftSummaryStore`, `BusinessDayTotalsReader` (iade edilmiş iptal hesap).
- Tables: `PostgresTableReleasePort` (işaretçi temizliği, sıradaki hesap seçimi), kasaya gönderilmiş hesap ayrımı `NotSentBill` (devir, birleştirme, işaretçi izdüşümü). Bulgu: sıralamada `NULL DESC` birleşik hesabı öne alıyordu; `COALESCE` ile düzeltildi (test kırmızıydı).
- `BillCancellationStore`: PO kararı (2) uygulandı; "siparişi de iptal et" hesabın bütün kaynak siparişlerini iptal eder (her sipariş mevcut sipariş iptal akışından geçer: hazırlanmış kalem fire, hazırlanmamış stoğa döner), sonuç `CancelledOrderIds` taşır ve her sipariş için kasa kuyruğu duyurulur. Tek siparişin iptali o siparişi içeren birleşik hesabı bütünüyle iptal eder, diğer siparişler açık kalır, kalemleri serbest kalır. Ödenmiş/parası hareket etmiş birleşik hesabın hiçbir siparişi iptal edilemez.
- `BillingSplitStore`: kalem kısıtı (`bill_items_order_item_id_key`) ihlali de yarış kurtarması sayılır.

## Doğrulama
- Kırmızı (`red*.log`): yeni testler çözümler olmadan düşüyor (Host 8/9, Tables 3, BillFoundation, SecurityAdministration, Reporting, ShiftSummary, Recall, Release, Comp). Dokuzuncu test (bayraksız hesap iptali) korumadır, önce de geçer.
- Yeşil (`final-*.log`): BillFoundation 51, Tables modülleri 42+46+23+62+13+34, Host Tables 40, SecurityAdministration 44, Reporting 31, Billing 245 (+3 sonradan), Orders Comp/Confirmation/TableDraft/Void/VoidSent 30/39/126/5/35, CustomerAccounts 22, CashSession 106, PaymentTender 92, mimari sınır 31, API sözleşmeleri 17; hepsi exit 0.
- Mutasyon (`mutations.log`, `cmp` ile geri alındı): depo (3 yer), yerleşim ilk siparişle sınırlı, süpürücü (EXISTS), para koruması, askıda temizlik, geri alma para denetimi, kuyruk (2), vardiya, hesap iptali, masa işaretçisi sıralaması, Tables (devir, birleştirme, izdüşüm), gün toplamı, bekleyen sipariş araçları: testler düşüyor.

## Mutasyonu düşürmeyen yerler (denk veya gözlenemez)
- `BillClosureSweeper` "açık hesap yok" koşulu: sonucu değiştirmez (aday olsa bile `CompleteForPaidBillAsync` hesapları depodan okuyup tamamlamaz); denk mutant.
- `ClearOrderBillPointersAsync`: sipariş iptalinde işaretçiyi zaten `ReleaseOrderAsync` da temizliyor; denk mutant.
- `OrderCleanupStore.EmptyDraft` bağı: birleşik hesaba kalemi giren sipariş boş taslak değildir; denk mutant.
- `BillMoneyGuard.LockOrderBillsAsync` bağı: yalnız kilit; eşzamanlılık harnesi olmadan gözlenemez, test yok.
- `BillingSplitStore` kısıt yarışı kurtarması: iki istek yarışı gerektirir, test yok.

## [C] sınıfı (rapor ve gösterim) kararları
- `CustomerDisplayPaymentProgress`: birleşik hesap, iki siparişin müşteri ekranında da hesabın bütün tutarıyla görünür (hesap tek, ödeme tek). Kabul.
- `DualScreenStore.BindBillOrderToDisplayAsync`: siparişi olmayan hesap müşteri ekranına bağlanmaz (false döner). Kabul, sonraya.
- Kasa kuyruğu: birleşik hesap iki satırda da görünür, aynı hesap açılır; ödenince ikisi de kuyruktan düşer.
- `ChannelReportService` (iade): QR siparişleri masa siparişi olduğundan birleştirilebilirdi ve birleşik hesabın iadesi raporda görünmezdi. Karar: V1-RMD-846 birleştirmeyi yalnız Cashier/Waiter kaynaklı siparişlere açar (QR ve çevrimiçi siparişler reddedilir); böylece kanal raporu etkilenmez. `OrderInvoiceEndpoints` ve çevrimiçi sipariş tüketicileri de aynı nedenle etkilenmez.

## Koordinatöre
- İade edilmiş birleşik hesabın vardiya/gün toplamı artık iki siparişi birden dışarıda bırakır (tek hesap tek iade).
- `CancelBillResultV1` isteğe bağlı `CancelledOrderIds` alanı kazandı (eski istemciler etkilenmez).

## İnceleme
- qa-uctan-uca-akis (1): kilit sırası (çok siparişli iptalde hesaplar tek sıralı sorguyla kilitleniyor; siparişler de kimlik sırasıyla iptal ediliyor) düzeltildi. Kanal raporu iadesi (`ChannelReportService`): birleşik hesabın iadesi iki siparişe nasıl bölüneceği ürün kararı olduğundan kapsam dışı bırakıldı, bkz. koordinatöre not. Müşteri ekranı bağlama ve kuyruk görünümü [C] kararı olarak yukarıda. Kopya `Matches` tanımlarının eşitliğini zorlayan test yok (Tables sürümü internal).
- qa-backend (2): kısmi tamamlama dönüşü düzeltildi (süpürücü ve bekleyen sipariş aracı artık siparişi tek tek tamamlar: `CompleteOrderWhenSettledAsync`; birleşik hesabın bir siparişi tamamlanıp diğerinin başka açık hesabı varsa yalnız o tamamlanır, sayaç doğru); çok siparişli iptalin geri alınması, ödenmiş birleşik hesapta bayraklı iptalin engeli ve kısmi tamamlama testleri eklendi (`green-after-review.log`, Security `green-security-after.log`). Tek siparişin iptalinde kardeş siparişin kuyruk duyurusu: sipariş iptal ucunun duyurusu yalnız iptal edilen siparişi kapsıyor; kardeş siparişler bir sonraki yenilemede düşer, [C] olarak bırakıldı.
- qa-bulgu-dogrulayici: kilit sırası tutarlı (845 yeni çapraz kilit getirmiyor); kanal raporu bulgusu geçerli, 846'da QR/çevrimiçi dışlanarak kapatılıyor; önceden var olan risk: masa birleştirme/devir önce masa satırını sonra hesap kilidini alıyor, sipariş iptali ve geri çağırma ters sırada; aynı masada eşzamanlı olursa Postgres 40P01 ile birini düşürür (845 getirmedi, ayrı görev önerilir).
