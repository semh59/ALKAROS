# Kalan İşler — Derin Çalışma Planı (Remaining Work Plan)

Tarih: 2026-08-12
Kaynak: plan/** task metadata taraması (269 task), `plan/GATES.md`, topolojik
dalga hesabı (dependency closure) ve kritik yol analizi.
Kapsam: `Done` olmayan tüm görevlerin (170 `Planned` + 13 `Blocked`) sıralı,
kanıta dayalı yürütme planı.

## 1. Genel durum özeti

| Boyut | Değer | Açıklama |
| --- | --- | --- |
| Toplam görev | 269 | plan/** altındaki tüm task dosyaları |
| Done | 86 | V0-EXIT; V1 temel katmanları (FND/IAM/SEC/CAT; V1-OPS-001/002 hariç), V0 karar katmanları (DOM/DAT/ARC), V1-TBL-001, V1-FND-024 dahil |
| Planned | 170 | Bu planın ana kapsamı |
| Blocked | 13 | 10 Blocked V0 devir (GATES.md `V0_DEFERRED_TASKS` içinde Blocked olmayan tek görev `V0-SEC-001`, Done) + V12-FSC-001 (fiscal karar) + V20-INT-004 (meal-card) + V20-LIC-001 (lisans kararı) |
| NotApplicable | 0 | Karar bekleyen devirler hiçbirinde `NotApplicable` değil |
| Gate zinciri | V0 → V1 → V11 → V12 → V13 → V14 → V15 → V20 | GATES.md |

Milestone görünümü (dalga aralıkları gate başına):

| Gate | Görev aralığı (dalga) | Bağımlı olduğu gate |
| --- | --- | --- |
| GATE-V1-EXIT | Dalga 1–6 (V1.0 işleri; dalga 7+ aynı anda) | GATE-V0-EXIT (kapalı) |
| GATE-V11-EXIT | Dalga 1–9 (V11 görevleri) | GATE-V1-EXIT |
| GATE-V12-EXIT | Dalga 1–13 (V12 görevleri + V0-HUG/MCD/CMP devirleri) | GATE-V11-EXIT |
| GATE-V13-EXIT | Dalga 1–14 (V13 görevleri + V0-QNB devri) | GATE-V12-EXIT |
| GATE-V14-EXIT | Dalga 2–14 (V14 görevleri + V0-YSP/PRN/QRG/SEC devirleri) | GATE-V13-EXIT |
| GATE-V15-EXIT | Dalga 1–18 (V15 görevleri + V0-BKP devirleri) | GATE-V14-EXIT |
| GATE-V20-EXIT | Dalga 5–27 (V20 görevleri + V0-LIC devri) | GATE-V15-EXIT |

## 2. Yöntem: topolojik dalgalar

Sıralama, görev dosyalarındaki `Dependencies` alanlarından türetilmiştir.
"Kapalı" küme = Done + NotApplicable; her adımda tüm dependency'leri kapalı
olan görevler bir sonraki dalgayı oluşturur. Sonuç: **27 dalga, ulaşılamaz
görev yok** (tüm 183 görev bir dalgaya atandı).

Dalga sırası başlatma emri verir; ancak bir görev yalnız kendi dalgasında
değil, tüm dependency'leri kapalı olduğu anda başlayabilir (paralellik için
aynı anlama gelen dalgalar aynı anda yürütülebilir).

En çok tüketilen bağımlılıklar (darboğaz riski):

| Bağımlılık | Tüketici sayısı | Not |
| --- | --- | --- |
| `V0-CMP-005` (erişilebilirlik) | 20 | Done; UI görevlerinin ortak girdisi |
| `V1-SEC-002` (sensitive payload) | 20 | Done; entegrasyon görevlerinin ortak girdisi |
| `V1-FND-005` (transaction outbox) | 17 | Done; durable workflow'ların altyapısı |
| `V1-IAM-002` (authorization) | 15 | Done; izin denetimlerinin altyapısı |
| `V0-CMP-003` (KVKK envanteri) | 13 | Done; PII görevlerinin ortak girdisi |
| `V0-DOM-001` (domain kararları) | 13 | Done |
| `V1-ORD-001` | 13 | **Planlanmış**; dalga 1'de öncelikli |
| `V1-SEC-001` (secret boundary) | 13 | Done |
| `V0-DAT-002` (data decisions) | 11 | Done |

## 3. Kritik yol (en derin zincir, 27 adım)

Topolojik derinlik 27; en uzun zincir V11 tarafından V20'ye uzanır:

```text
V11-UNT-001 → V11-INV-004 → V11-INV-001 → V11-INV-002 → V11-RSV-001 →
V11-INV-007 → V11-RSV-003 → V12-ALC-003 → V12-HUG-003 → V12-ALC-004 →
V12-FSC-002 → V12-REC-001 → V13-QNB-004 → V15-REC-001 → V15-OBS-002 →
V15-PER-002 → V15-RUN-001 → V20-DOC-002 → V20-REL-001 → V20-MIG-001 →
V20-MIG-002 → V20-UAT-003 → V20-REL-002 → V20-GAT-002 → V20-REL-003 →
V20-REL-004 → V20-REL-005
```

Kritik yol yorumu:

- Zincir **V11 envanter/rezervasyon** ile başlar; `V11-UNT-001` (birim sistemi)
  kimsenin beklemeyeceği kadar erken bir darboğazdır (dalga 1).
- Orta halka **V12-HUG-003 / V12-ALC-004 / V12-FSC-002** `V0-HUG-001` ve
  `V0-CMP-001` dış kanıtına bağlıdır: Hugin kontratı olmadan bu dalgalar
  (9–11) duramaz.
- `V13-QNB-004` `V0-QNB-001` (V13 devri) kanıtına bağlıdır; QNB kontratı
  olmadan V13 ve V15–V20 mutabakat zinciri ilerlemez.
- `V20-REL-*` zinciri mutlak kuyruk: milestone hedefi signed Approve
  (`V20-REL-003`) ve production deployment (`V20-REL-004`).

Bu zincire paralel ikinci kritik kol (V1 uygulama): `V1-ORD-001 → V1-ORD-002 →
V1-CUI-002` (cashier UI kolu) ve `V1-ORD-001 → V1-KIT-001 → ... → V1-KIT-004 →
V20-INT-005`. V20-UAT zinciri bu kollardan `V20-INT-003/005/006` (`V20-UAT-001`)
ile `V20-INT-001/002/004` (`V20-UAT-002`) üzerinden beslenir; `V20-UAT-003`
bunların ardından gelir.

## 4. Blocker'lar ve dış kanıt gereksinimleri

Devredilen 11 V0 görevi `GATE-V0-EXIT`'te (2026-08-04, C41) kapsam dışı
bırakıldı; her biri yeniden açılma aşamasında gerçek kanıtla kapanır
(GATES.md `V0_DEFERRED_TASKS`):

| Görev | Yeniden açılma | Gerekli dış kanıt | Kritik tüketiciler |
| --- | --- | --- | --- |
| `V0-HUG-001` | V12 | Gerçek Hugin provider contract / erişim | V12-HUG-001..004, V12-FSC-004, V20-INT-001 |
| `V0-QNB-001` | V13 | Gerçek QNB test tenant / contract | V13-QNB-001..005, V12-FSC-005, V20-INT-002 |
| `V0-YSP-001` | V12 | Yemeksepeti Partner contract / webhook | V14-ONL-001..005, V14-MAP-001..002, V20-INT-003 |
| `V0-MCD-001` | V12 | Approved provider listesi + her provider kontratı | V12-MCD-001..004, V20-INT-004 |
| `V0-PRN-001` | V14 | Onaylı printer model listesi + gerçek cihaz transcript | V1-KIT-003/004, V20-INT-005 |
| `V0-QRG-001` | V14 | Non-production relay/domain + TLS + erişim | V14-QRS-001..003, V14-QRT-001, V20-INT-006 |
| `V0-CMP-001` | V12 | Mali müşavir onaylı fiscal strateji (T300 veya QNB) | V12-FSC-001..005, V20-CMP-001 |
| `V0-LIC-001` | V20 | Gerçek license server ve lisans sözleşmesi | V20-LIC-001/002 |
| `V0-BKP-001` | V15 | İkinci PostgreSQL 18 instance/cihaz kanıtı | V1-OPS-002 (dalga 2), V15-BKP-001/002, V20-DRL-001 |
| `V0-BKP-002` | V15 | Gerçek yedekleme donanımı + RPO/RTO hedefleri | V15-BKP-001/002, V15-RUN-001, V20-MIG-001 |
| `V0-SEC-001` | V14 | *(GATES.md deferral: Not V0 gate closure evidence; task dosyası mevcut ve Done — ASVS hedef seviyesi kararı, detay çizelgesi)* | V20-SEC-001 |

Ek Blocked görevler:

| Görev | Blocker | Çözüm koşulu |
| --- | --- | --- |
| `V12-FSC-001` | GİB applicability + seçili fiscal strateji + private contract kanıtları eksik; gapless numara davranışı için doğrulanmış kaynak yok | `V0-CMP-001` kararı: T300 veya QNB branch seçilir; dalga 2'de bekler |
| `V20-INT-004` | `V0-MCD-001` approved provider listesi üretmedi | Liste + provider başına legal code |
| `V20-LIC-001` | `V0-LIC-001` henüz Required/NotApplicable kararı üretmedi | Karar gelince `Planned` veya `NotApplicable` |

**Aksiyon:** Bu 11 dış kanıt, planın başında (dalga 1 paralelinde) temin
edilmelidir; aksi halde dalga 2–11'de kritik fiskal/payment görevleri durabilir.

## 5. Dalga detayları

Fragmanlar `Dependencies` closure'ıyla üretilmiştir; amaç metinleri task
dosyalarından alınmıştır.

### Dalga 1 — V0/Q1 çekirdek + V1 uygulama temeli

| Görev | Amaç |
| --- | --- |
| `V0-BKP-001` | Disposable PostgreSQL 18 instance üzerinde backup, checksum ve restore tool path uygulanabilirliğini doğrulamak. |
| `V0-CMP-001` | Hedef restoran profilinin YN ÖKC, adisyon/e-Adisyon ve 2026 GİB kuralları kapsamındaki yükümlülüklerini yazılı olarak doğrulamak. |
| `V0-LIC-001` | Tek seferlik license activation, machine binding, offline authorization, transfer, support update ve failure davranışını tanımlamak. |
| `V0-MCD-001` | Desteklenecek meal-card provider'larını belirlemek ve payment, cancellation/refund, commission, statement ve settlement contract'ını yazmak. |
| `V0-PRN-001` | İki mutfak yazıcısının bağlantı, durum algılama, retry ve fiziksel duplicate davranışını gerçek cihazla belirlemek. |
| `V0-QRG-001` | Public QR trafiğinin local POS'a inbound LAN erişimi açmadan taşınabileceğini kanıtlamak. |
| `V0-YSP-001` | Partner erişimi, webhook kimliği, retry, order status, cancellation ve catalog mapping sözleşmesini doğrulamak. |
| `V1-ALT-001` | Rule-based Alert lifecycle, source reference, deduplication ve notification audit davranışını uygulamak. |
| `V1-CSH-001` | Payment'ı etkinleştirmeden terminal/cashier ownership, tek open session, cash routing ve close permission sözleşmesini kesinleştirmek. |
| `V1-OBS-001` | V1 flow'ları için structured event contract, correlation/request ID ve bounded status-audit persistence eklemek. |
| `V1-OPS-001` | Actor, reason, correlation ve before/after reference alanlarıyla V1 critical command'ları için immutable audit event üretmek. |
| `V1-ORD-001` | Order ve OrderItem lifecycle, price snapshot, modifier ve Table/customer context davranışını uygulamak. |
| `V1-REC-001` | Canonical ReconciliationCase lifecycle, paired source reference, open-case deduplication ve append-only event/action yapısını uygulamak. |
| `V1-SET-001` | Module owner, scope, type ve append-only change history ile validated non-secret setting'leri kalıcılaştırmak. |
| `V1-TBL-004` | `Table.Reserved` arkasındaki onaylı actor, reason ve expiry modelini kalıcılaştırmak. |
| `V1-WTR-001` | Personal device session, installable shell ve izinli offline operation queue davranışını uygulamak. |
| `V11-MNU-003` | Price veya stock ownership almadan Catalog Product seçen reusable Menu/MenuItem composition modelini uygulamak. |
| `V11-PUR-002` | KVKK/veri minimizasyon kuralları kapsamında minimum tedarikçi kimliğini, vergi/iletişim verilerini, aktif durumu ve benzersizliği uygulamak. |
| `V11-UNT-001` | Boyutlar arası ve tutarsız döngüleri reddeden birim tanımlarını, boyutları ve deterministik dönüşümleri uygulamak. |
| `V12-PAY-001` | V0 finansal sözleşmeleri kapsamında payment kimliğini, kanonik status geçişlerini ve para alanlarını uygulamak. |
| `V13-CST-001` | Field-level access policy ile PII sahibi boundary içinde minimum customer identity, tax ve contact alanlarını kalıcılaştırmak. |
| `V15-PER-001` | Tanımlanan eş zamanlılık kapsamında order gönderimini, son bölüm rezervasyonunu, payment kapanışını ve webhook alımını ölçmek. |
| `V15-SEC-001` | V1-SEC-001 secret boundary üzerinde production rotation, failover ve recovery davranışını uygulamak. |

İlk görev dalgası — sıra önerisi: `V1-ORD-001` (13 tüketici), `V11-UNT-001`
(kritik yol başı), `V12-PAY-001` (payment kimliği), ardından UI/V1 görevleri.

### Dalga 2

| Görev | Amaç |
| --- | --- |
| `V0-BKP-002` | İşletmenin veri kaybı ve kesinti toleransını ölçülebilir RPO/RTO acceptance target değerlerine dönüştürmek. |
| `V0-HUG-001` | Seçimi yeniden açmadan T300 payment, fiscal, timeout, unknown, cancellation, refund ve reconciliation sözleşmesini model/firmware kanıtıyla doğrulamak. |
| `V0-QNB-001` | Outgoing/incoming e-belge, registered-user query, idempotency, status query ve timeout sözleşmesini doğrulamak. |
| `V1-BIL-001` | Bill, BillItem ve V0-DOM-002 tarafından seçilen referentially safe Order/OrderItem source ilişkisini uygulamak. |
| `V1-KIT-001` | Accepted Order'lardan station-scoped KitchenTicket üretmek ve KitchenTicketItem status'lerini bağımsız korumak. |
| `V1-OPS-002` | Local database backup'ını schedule etmek, metadata'yı kalıcılaştırmak ve database/disk/backup health durumlarını yayımlamak. |
| `V1-ORD-002` | Waiter/cashier submit akışını response replay içeren version-controlled concurrent command olarak uygulamak. |
| `V11-INV-004` | Stok kimliklerini, stok türlerini, takip edilen birim ve konum yapılandırmasını uygulamak. |
| `V11-RCP-001` | Operasyonel kullanımdan sonra değişmezlik ile tarifleri, sürüm oluşturmayı, etkinleştirmeyi ve kullanımdan kaldırmayı uygulamak. |
| `V12-FSC-001` | Provider/device reference ve immutable request history ile sale, cancellation ve refund FiscalDocument kayıtlarını kalıcılaştırmak. |
| `V12-PAY-002` | Tender request/handler contract'ını tanımlamak; kayıtlı olmayan yöntemleri ve CustomerAccount yöntemini V1.3'e kadar typed version ile bırakmak. |
| `V13-ACC-001` | Açık yön semantiği ve değişmez kaynak bağlantılarıyla pozitif büyüklükteki hesap işlemlerini sürdürmek. |
| `V13-CST-002` | Legal olarak korunan financial reference'ları silmeden Requested, RetentionBlocked, Pending ve Anonymized durumlarını uygulamak. |
| `V14-MAP-001` | Provider ürün/değiştirici tanımlayıcılarını, açık eşlenmemiş davranışa sahip etkin dahili katalog öğeleriyle eşlemek. |
| `V14-MAP-002` | Doğrulanan her Yemeksepeti status'ünü izinli internal command, explicit no-op veya typed unknown-status evidence sonucuna eşlemek. |
| `V14-ONL-001` | Eşzamansız işlemden önce her provider event'inin kimliğini bir kez doğrulamak ve kalıcı hale getirmek. |
| `V14-QRS-001` | Reusable raw secret saklamadan hashed, revocable ve time/policy-bound Table token yayımlamak. |
| `V15-SEC-002` | Oturum açma kısıtlaması, kilitleme politikası, oturum rotasyonu ve idari iptal eklemek. |
| `V15-SEC-003` | V1-SEC-002 sınırı üzerinde retention enforcement, authorized re-encryption ve deletion scheduling uygulamak. |
| `V20-LIC-001` | Yalnız V0-LIC-001 sonucu Required ise onaylanan license enforcement davranışını uygulamak. |

### Dalga 3

| Görev | Amaç |
| --- | --- |
| `V1-BIL-002` | Payment execution'ı etkinleştirmeden item, quantity ve amount ownership segmentlerini kalıcılaştırmak. |
| `V1-BIL-003` | Yalnız onaylanmış discount, fee ve tip line type'larını tax ve authorization kurallarıyla hesaplamak ve kalıcılaştırmak. |
| `V1-KIT-002` | Her kitchen item'ı tam bir configured station/printer route'a veya açık configuration error sonucuna çözmek. |
| `V1-ORD-003` | Onaylı void/complimentary politikasını permission, reason, audit ve kitchen-state kontrolleriyle uygulamak. |
| `V1-TBL-002` | History'yi koruyarak open operational Order/Bill ilişkisini Table'lar arasında taşımak. |
| `V1-TBL-003` | Source Table veya Order silmeden multi-table merge membership ve explicit undo modelini uygulamak. |
| `V1-WTR-002` | Waiter permission kapsamında Table seçimi, Product/modifier/note girişi ve idempotent submit akışını uygulamak. |
| `V1-WTR-003` | Server-authoritative Order ve KitchenTicketItem progress durumunu reconnect-safe refresh ile göstermek. |
| `V11-INV-001` | Tiplendirilmiş stok hareketlerini pozitif büyüklük, yön kuralları ve kaynak referanslarıyla uygulamak. |
| `V11-MNU-001` | İş tarihi menüsü oluşturma, ürün seçimi, günlük fiyat ve açma/kapama kurallarını uygulamak. |
| `V12-CSH-001` | Terminal/cashier bağlı Open, Counting, Closing, Closed ve Reconciled CashSession geçişlerini uygulamak. |
| `V12-FSC-004` | Yalnız `V0-CMP-001` T300 adisyon lifecycle'ını seçtiğinde, doğrulanmış V0-HUG-001 contract'ındaki open/update/close command map'ini uygulamak. |
| `V12-FSC-005` | Yalnız `V0-CMP-001` QNB e-Adisyon lifecycle'ını seçip `V0-QNB-001` exact private/public contract'ı doğruladığında open/update/close map'ini uygulamak. |
| `V12-HUG-001` | Doğrulanmış T300 contract'ına karşı onaylanmış ve reddedilen kart payment akışlarını uygulamak. |
| `V12-MCD-001` | Onaylanmış bir MealCard payment için provider, gross, commission, deduction ve net receivable alanlarını kalıcılaştırmak. |
| `V13-ACC-002` | Değişmez hesap defterinden mevcut bakiyeyi ve tarihli anlık görüntüleri hesaplamak. |
| `V13-ACC-004` | AccountPayment'ın kimliğini, method'unu, amount'unu ve durable Requested/Approved/Declined/Unknown durum geçişlerini kalıcılaştırmak. |
| `V13-QNB-001` | Doğrulanmış QNB contract'ı kullanarak zaman sınırlı e-Fatura kaydını status sorgulamak ve önbelleğe almak. |
| `V13-QNB-003` | Gelen provider belgelerini bir kez özel, değişmez alım kayıtlarına almak. |
| `V14-QRS-002` | Relay message authentication yapmak ve local command dispatch öncesi replay, rate-limit ve payload-size kontrollerini uygulamak. |
| `V15-BKP-001` | Doğrulanmış şifrelenmiş veritabanı yapılarını, saklama ve anahtar meta verileriyle birlikte doğrulanmış hedefe yüklemek. |
| `V15-KVK-001` | Onaylanan veri envanterini değerlendirmek ve tüm mağazalarda uygun silme/anonimleştirme işlemlerini planlamak. |
| `V15-OBS-001` | Critical flow'larda correlation, request, user/device ve provider reference alanlarını redaction kurallarıyla structured log olarak yazmak. |

### Dalga 4

| Görev | Amaç |
| --- | --- |
| `V1-KIT-003` | Ticket/output başına tek logical PrintJob kalıcılaştırmak ve retry'ları idempotency altyapısıyla yürütmek. |
| `V1-TBL-005` | Authoritative source ilişkilerinden current Order/Bill pointer projection'larını üretmek ve rebuild etmek. |
| `V11-INV-002` | StockMovement ledger'dan location/item bazında authoritative on-hand balance projection'ını üretmek ve rebuild etmek. |
| `V11-PUR-001` | Supplier PurchaseOrder ve line item'ları, StockLedger'a kayıtlı receipt movement'larıyla uygulamak. |
| `V11-UI-001` | Statik/günlük menü ve değişmez tarif versiyonu oluşturma/aktivasyon için Türkçe ekranları uygulamak. |
| `V12-ALC-001` | Payment/Bill/segment identity, currency, amount ve idempotency için PaymentAllocation row'larını ve database enforcement'ı uygulamak. |
| `V12-CSH-002` | Cash sale/refund/in/out entry'lerini kaydetmek ve expected/actual close variance değerini hesaplamak. |
| `V12-FSC-003` | V0-CMP-001 tarafından seçilen tam olarak bir adisyon branch'ini fail-closed registry'de etkinleştirmek. |
| `V12-HUG-002` | Timeout veya connection loss sonucunu Unknown olarak saklamak, terminal status'ünü sorgulamak ve çözümlenemeyen divergence evidence'ı üretmek. |
| `V12-MCD-002` | Meal-card payment'larını provider settlement dönemlerinde gruplamak, parent/child durumunu atomik güncellemek ve mismatch evidence'ı üretmek. |
| `V13-ACC-009` | Bill'den bağımsız bir AccountReceipt'i, onu doğrulayan AccountPayment'a bağlayarak kaydetmek. |
| `V14-QRS-003` | Raw Table token'ı reusable browser credential'a çevirmeden QR token validation sonrası revocable customer session oluşturmak. |
| `V14-QRT-001` | `V0-ARC-009` tarafından seçilen public gateway, local outbound connector ve durable outage queue topology'sini uygulamak. |
| `V15-BKP-002` | Isolated PostgreSQL instance'a restore işlemini otomatikleştirmek ve integrity/application smoke kontrollerini çalıştırmak. |
| `V15-KVK-002` | Onaylı PII anonymization işlemini idempotent, resumable ve store-checkpoint tabanlı workflow olarak uygulamak. |
| `V15-SUP-001` | Gizli bilgileri, payment verilerini veya gereksiz kişisel verileri dışarı aktarmadan olayları teşhis eden sınırlı bir destek paketi uygulamak. |

### Dalga 5

| Görev | Amaç |
| --- | --- |
| `V1-CUI-001` | Türkçe cashier shell, authenticated session ve concurrency-aware Table status görünümünü uygulamak. |
| `V1-KIT-004` | Send/ack crash window'u explicit Unknown state ve operator-controlled reprint semantiğiyle yönetmek. |
| `V1-RPT-001` | Onaylanmış ölçüm sözleşmelerini kullanarak order, table, garson ve yazdırma hatası raporlarını uygulamak. |
| `V11-INV-003` | Tam original movement'a bağlı tek bir idempotent `Reversal` movement oluşturmak. |
| `V11-INV-005` | Bakiyeleri doğrudan düzenlemeden, zorunlu gerekçeyle izin verilen Ayarlama hareketlerini yayınlamak ve denetlemek. |
| `V11-INV-006` | Production'den, porsiyon rezervasyonundan veya manuel onaylı kaynaktan izlenebilir Atık hareketlerini kaydetmek. |
| `V11-RCP-002` | Geçmiş tarif maliyetini yeniden oluşturmak için gereken içerik düzeyindeki maliyet esasını sürdürmek. |
| `V11-RSV-001` | Bir OrderItem ve StockBalance'a bağlı `Reserved`, `Released`, `Consumed` ve `Wasted` geçişlerini uygulamak. |
| `V12-ALC-002` | Allocated, paid ve change total değerlerini hesaplamak ve PaymentSatisfied projection'ını authoritative Payment kayıtlarından üretmek. |
| `V12-CSH-003` | Cash tender için Payment, PaymentAllocation, CashTransaction ve change sonucunu tek transaction içinde oluşturmak. |
| `V12-PAY-004` | Approved BankCard sonucu, PaymentAllocation ve fiscal request geçişini crash-safe durable workflow ile tamamlamak. |
| `V12-PUI-002` | Aktif terminal/kasiyer için açma, sayma, kapatma ve fark teyit akışını uygulamak. |
| `V13-ACC-003` | Onaylanmış bir CustomerAccount tender'ını çift kayıt oluşturmadan tek AccountCharge ve PaymentAllocation kaydına dönüştürmek. |
| `V13-ACC-005` | Açık CashSession içinde Bill'den bağımsız Payment, cash AccountPayment, CashTransaction ve Payment AccountTransaction kayıtlarını oluşturmak. |
| `V13-ACC-006` | Bill'den bağımsız BankCard AccountPayment'i doğrulanmış Hugin sonucuyla crash-safe tamamlamak. |
| `V14-QRO-001` | Kimliği doğrulanmış bir QR gönderimini PendingConfirmation'daki bir dahili Order'e dönüştürmek. |
| `V20-INS-001` | Signed release candidate'ı deterministic ve belgelenmiş package ile clean supported target'a kurmak. |

### Dalga 6

| Görev | Amaç |
| --- | --- |
| `V1-CUI-002` | Product/modifier seçimi, note, Draft düzenleme ve idempotent submit akışını Türkçe UI ile uygulamak. |
| `V1-CUI-003` | Open Order/Bill, kitchen progress ve failed/Unknown PrintJob durumlarını izinli recovery action'larla göstermek. |
| `V11-INV-007` | On-hand projection ve authoritative PortionReservation lifecycle'ından reserved ve available balance değerlerini üretmek ve rebuild etmek. |
| `V11-PRD-001` | Immutable RecipeVersion'a bağlı Planned, InProgress, Completed ve Cancelled ProductionBatch lifecycle'ını uygulamak. |
| `V12-TBL-001` | Payment durumu bulunan Bill için table transfer, merge ve bill mutation işlemlerini fail-closed policy ile yönetmek. |
| `V13-ACC-007` | AccountPayment, cash/provider evidence ve AccountTransaction kaynakları farklılaştığında tekilleştirilmiş ReconciliationCase oluşturmak. |
| `V13-INV-001` | Bakiyeyi değiştirmeden kapalı bir fatura dönemi için uygun faturalanmamış CustomerAccount işlemlerini seçmek. |
| `V13-PUR-001` | Envanteri iki kez değiştirmeden, gelen invoice satırlarını tedarikçi, satın alma makbuzu ve borç hesabı girişleriyle eşleştirmek. |
| `V14-QRO-002` | Uzaktan QR hizmet reddine izin vermeden onaylı dolu/ayrılmış/değişiklik yok table davranışını uygulamak. |
| `V20-DRL-001` | Release adayını onaylanmış tesis dışı yedeklemeden yalıtılmış, temiz bir ortama geri yüklemek ve kurtarma hedeflerini ölçmek. |
| `V20-INS-002` | Onaylı önceki kurulumu release candidate'a yükseltmek ve update migration öncesi/sonrası failure durumundan güvenle kurtarmak. |
| `V20-INT-005` | Onaylanan her yazıcı modelini ve aktarımını yönlendirme, kodlama, kağıt hatası ve retry davranışı açısından onaylamak. |

### Dalga 7

| Görev | Amaç |
| --- | --- |
| `V11-PRD-002` | ProductionBatch transaction'ında IngredientConsumption ve prepared-portion ProductionOutput movement'larını oluşturmak. |
| `V11-RSV-002` | Rakip kanalların aşırı satış yapmaması için satır kilitleme/sürüm kontrolleriyle porsiyonları ayırmak. |
| `V11-RSV-003` | Açık mutfak durumunu kullanarak mutfak öncesi iptali Release'e ve hazırlık sonrası iptali Waste'a çevirmek. |
| `V13-INV-002` | Onaylanmış GİB/QNB profili altında seçilen kaynak kümesinden invoice başlığını ve vergi gruplu satırları oluşturmak. |
| `V13-UI-003` | Gelen belge doğrulamayı, tedarikçi/makbuz eşleşmesini, fark incelemesini ve borç kaydını uygulamak. |

### Dalga 8

| Görev | Amaç |
| --- | --- |
| `V11-MNU-002` | Authoritative production/inventory kayıtlarından prepared, reserved, consumed, waste ve available counter projection'larını üretmek. |
| `V11-UI-002` | Tarif ve stok etkisi önizlemesi ile planned/start/complete/cancel production workflow'unu uygulamak. |
| `V12-ALC-003` | Full veya partial refund talebinin eligibility, target allocation, amount ve idempotency değerlerini RefundIntent olarak kalıcılaştırmak. |
| `V13-INV-003` | Her invoice satırını, onu üreten tam hesap işlem kümesiyle eşlemek. |
| `V14-STK-001` | Cashier, waiter, QR ve online channel için tek channel-neutral reservation command ve ortak last-portion arbitration sonucu sağlamak. |

### Dalga 9

| Görev | Amaç |
| --- | --- |
| `V11-RPT-001` | Satış oranı, porsiyon tüketimi, production, atık ve kritik stok raporlarını uygulamak. |
| `V11-UI-003` | İzin verilen gerekçelerle stok bakiyesi, satın alma fişi, ayarlama ve atık ekranlarını uygulamak. |
| `V12-HUG-003` | Onaylı RefundIntent için iptal/iade işlemini gönderip Approved, Rejected veya Unknown provider sonucunu kaydetmek. |
| `V12-MCD-003` | Provider-neutral meal-card adapter SPI, registry ve capability rejection contract'ını oluşturmak. |
| `V13-QNB-002` | Değişmez bir invoice draft'ını anında göndermek ve provider referanslarını/status geçmişini sürdürmek. |
| `V13-RPT-001` | Hesap yaşlandırma, invoice yaşlandırma/status, gelen eşleşme ve tedarikçi borç raporlarını uygulamak. |
| `V14-CWB-001` | Authenticated QR customer session için available sellable menu'yu internal management verisini açmadan sunmak. |
| `V14-ONL-002` | Kabul edilen webhook verisini tek provider kaydına ve tek dahili Accepted Order'a idempotent olarak bağlamak. |
| `V14-ONL-004` | Onaylanan menü/ürün projeksiyonunu, deterministik harici tanımlayıcılarla etkinleştirilmiş her çevrimiçi-order kanalına yayınlamak. |
| `V14-QRO-003` | Bekleyen bir QR order'ını onaylamak veya reddetmek ve bölümleri yalnızca başarılı kabul üzerine ayırmak. |

### Dalga 10

| Görev | Amaç |
| --- | --- |
| `V12-ALC-004` | Yalnız provider Approved refund sonucundan sonra compensating allocation ve fiscal refund handoff'unu finalize etmek. |
| `V12-MCD-004` | Approved meal-card provider sonucunu tek PaymentAllocation ve fiscal handoff'a provider-neutral durable workflow ile bağlamak. |
| `V13-INV-004` | Issued Invoice'ı silmeden veya Account balance'ı değiştirmeden izinli cancellation/correction intent'ini temsil etmek. |
| `V14-CWB-002` | QR customer'ın açık final summary ile Order oluşturup PendingConfirmation workflow'una göndermesini sağlamak. |
| `V14-ONL-003` | Provider status/cancellation değişikliklerini race-safe local transition ile işlemek ve çözümlenemeyen divergence evidence'ı üretmek. |
| `V14-ONL-005` | Tek onaylı kullanılabilirlik projeksiyonundan satılabilir veya kullanılamıyor durumunu etkin çevrimiçi kanallara yayınlamak. |

### Dalga 11

| Görev | Amaç |
| --- | --- |
| `V12-FSC-002` | Fiscal kapsamındaki bir Bill'in ne zaman close edilebileceğine veya reconciliation gerektirdiğine onaylı legal/device policy ile karar vermek. |
| `V12-PAY-003` | Cash handler, durable BankCard workflow ve MealCard provider-registry bridge'ini tek fail-closed registry'de kaydetmek. |
| `V12-RPT-001` | Payment karışımı, cash oturumu, mali status ve yemek kartı kapatma raporlarını uygulamak. |
| `V13-QNB-005` | Yalnız `V0-QNB-001` kanıtında onaylanan QNB iptal/düzeltme işlemini eşlemek ve belirsiz sonuçları sorgulamak. |
| `V14-OUI-001` | Etki alanı komutlarını atlamadan QR bekleyen siparişler ve harici kanal siparişleri için yetkili personele bir operasyonel kuyruk sağlamak. |
| `V20-INT-003` | Gelen siparişler ve giden status, katalog ve kullanılabilirlik işlemleri için onaylanmış Yemeksepeti contract'ını onaylamak. |
| `V20-INT-004` | V0-MCD-001 çıktısından türetilen provider-specific V20-INT-1xx certification task'larının eksiksizliğini ve sonuçlarını gate olarak doğrulamak. |
| `V20-INT-006` | Onaylı network/security topology altında scan işleminden PendingConfirmation Order'a kadar public QR path'i sertifikalandırmak. |
| `V20-SEC-001` | Release candidate'ın authentication, authorization, public endpoint, secret ve sensitive-data kontrollerini bağımsız olarak değerlendirmek. |

### Dalga 12

| Görev | Amaç |
| --- | --- |
| `V12-PUI-001` | Açık Bill tahsisleri üzerine Cash, BankCard ve onaylı MealCard payment kompozisyonunu uygulamak. |
| `V12-REC-001` | V1.2 yetkili kaynakları farklılaştığında tekilleştirilmiş ReconciliationCase kayıtları oluşturmak. |
| `V13-ACC-008` | V1.2'de fail-closed kalan CustomerAccount tender handler'ını V1.3 composition extension üzerinden kaydetmek ve approved allocation'ı işlemek. |

### Dalga 13

| Görev | Amaç |
| --- | --- |
| `V12-HUG-004` | Yerel onaylı/iade edilmiş kart işlemlerini terminalin doğrulanmış toplamları veya işlem sorgu kaynağıyla karşılaştırmak. |
| `V12-PUI-003` | İzin verilen tam/kısmi geri ödeme, Bilinmeyen payment takibi ve mali/mutabakat status gösterimini uygulamak. |
| `V13-QNB-004` | Gönderim, cancellation/correction, local/provider status ve incoming retrieval farkları için reconciliation oluşturmak. |
| `V13-UI-001` | Alan izinleri altında müşteri profili, hesap defteri, bakiye/yaşlanma ve hesap payment ekranlarını uygulamak. |
| `V14-REC-001` | Local/provider Order, status, cancellation ve stock outcome farklılıklarını tespit etmek ve izlemek. |

### Dalga 14

| Görev | Amaç |
| --- | --- |
| `V13-UI-002` | Kaynak önizlemesini, kayıtlı kullanıcı sonucunu, draft incelemesini, gönderme/status ve workflow iptalini uygulamak. |
| `V14-RPT-001` | Onaylanan metrik tanımlarından QR ve çevrimiçi kanal hacim, değer, iptal ve mutabakat metriklerini raporlamak. |
| `V15-REC-001` | Payment, mali, QNB, çevrimiçi, yemek kartı, cash ve satın alma genelinde açık vakalar için tek bir okuma modeli oluşturmak. |
| `V20-DOC-001` | Onaylanan her operasyonel rol ve kurtarılabilir hata yolları için release ile eşleşen kullanıcı talimatlarını yayınlamak. |
| `V20-INT-001` | Onaylanan Hugin model/ürün yazılımı/protokol kombinasyonunu mali satış, retry, toplam ve arıza senaryolarına göre onaylamak. |
| `V20-INT-002` | Gerçek sandbox yanıtlarını ve mutabakat kanıtlarını kullanarak onaylanmış QNB ortamını ve belge yaşam döngüsünü onaylamak. |

### Dalga 15 — V15 kapanışı

| Görev | Amaç |
| --- | --- |
| `V15-OBS-002` | Veritabanı, disk, yazıcı, yedekleme ve entegrasyon durumunu tekilleştirilmiş uyarılarla değerlendirmek. |
| `V15-REC-002` | İzinli retry, accept-provider, accept-local, compensate, reject ve escalate action'larını permission ve audit ile yürütmek. |
| `V15-RPT-001` | Onaylanmış operasyonel, stok, payment, invoice ve kanal rapor sözleşmeleri üzerinde mutabakata varılmış bir raporlama giriş noktası uygulamak. |

### Dalga 16 — V15 kapanışı

| Görev | Amaç |
| --- | --- |
| `V15-NOT-001` | Tekilleştirme, üst kademeye yükseltme ve denetlenebilir sonuçlar içeren yapılandırılmış kanallar aracılığıyla onaylı operasyonel bildirimleri uygulamak. |
| `V15-OBS-003` | Korunan kayıtları silmeden health, alert-event, inbox/outbox ve high-volume audit support verisinin büyümesini retention/partition ile yönetmek. |
| `V15-PER-002` | Kritik işlem sınırlarında süreç, veritabanı, ağ, provider ve yazıcı hatalarını enjekte etmek. |

### Dalga 17 — V15 kapanışı

| Görev | Amaç |
| --- | --- |
| `V15-RUN-001` | Printer, Unknown payment, fiscal failure, backup, restore, disk ve provider outage olayları için yürütülebilir runbook'lar yazmak. |

### Dalga 18 — V15 kapanışı / V20 giriş

| Görev | Amaç |
| --- | --- |
| `V15-RUN-002` | Critical operational runbook'ları yazar müdahalesi olmadan test ortamında uygulayıp recovery sonuçlarını doğrulamak. |
| `V20-DOC-002` | Release ile eşleşen bir mimari, contract modülü, API/event, veri sahipliği, entegrasyon ve tanılama referansı yayınlamak. |
| `V20-LIC-002` | Veri kaybı olmadan lisansın sona ermesi, doğrulama hatası ve yetkili yenileme için onaylanmış operasyonel kurtarma yolunu kanıtlamak. |

### Dalga 19 — V20 giriş

| Görev | Amaç |
| --- | --- |
| `V20-REL-001` | Doğrulanmış ikili dosyalardan, yükleyici/güncelleyiciden, geçişlerden, yapılandırma şemasından ve belgelerden değişmez bir release paketi oluşturmak. |

### Dalga 20 — V20 kapanış

| Görev | Amaç |
| --- | --- |
| `V20-GAT-001` | Kapsam dahilindeki her PDF gereksiniminin ve kabul edilen her denetim düzeltmesinin uygulanmış, test edilmiş veya açıkça onaylanmış olduğunu doğrulamak. |
| `V20-MIG-001` | Representative sanitized dataset üzerinde production migration path'inin tamamını çalıştırmak ve integrity, duration ve resource sonuçlarını ölçmek. |
| `V20-UAT-001` | Release candidate üzerinde cashier, waiter, Table, Order, kitchen, QR, online-order operations ve printing workflow'ları için named-user acceptance testleri yürütmek. |
| `V20-UAT-002` | Billing, Payment, refund, CashSession, CustomerAccount, Invoice, purchasing, stock ve reporting workflow'ları için named-user acceptance testleri yürütmek. |

### Dalga 21 — V20 kapanış

| Görev | Amaç |
| --- | --- |
| `V20-CMP-001` | Uygulanan release'nin doğrulanmış mali, faturalama, saklama ve gizlilik uygulanabilirlik kararlarıyla eşleştiğine dair adlandırılmış onay almak. |
| `V20-MIG-002` | Taşınan release adayından migration öncesi kurtarılabilir durumuna onaylanmış geri alma yolunu kanıtlamak. |

### Dalga 22 — V20 kapanış

| Görev | Amaç |
| --- | --- |
| `V20-UAT-003` | Offline, timeout, duplicate, reconciliation, backup, diagnostics ve recovery prosedürleri için named operational acceptance testleri yürütmek. |

### Dalga 23 — V20 kapanış

| Görev | Amaç |
| --- | --- |
| `V20-REL-002` | Immutable release candidate'ı production-equivalent fakat non-production ortamda yalnız synthetic veya yetkili sanitized data ile pilot çalıştırmak. |

### Dalga 24 — V20 kapanış

| Görev | Amaç |
| --- | --- |
| `V20-GAT-002` | Tamamlanan gate çıktılarından, sonuçları yeniden yazmadan tamper-evident release evidence pack oluşturmak. |

### Dalga 25 — V20 kapanış

| Görev | Amaç |
| --- | --- |
| `V20-REL-003` | Exact immutable release candidate için evidence-backed approve veya reject kararını kaydetmek. |

### Dalga 26 — V20 kapanış

| Görev | Amaç |
| --- | --- |
| `V20-REL-004` | Yalnız signed Approve kararı verilen exact release artifact'ını kontrollü production deployment ile kurmak. |

### Dalga 27 — V20 kapanış

| Görev | Amaç |
| --- | --- |
| `V20-REL-005` | Onaylı gözlem penceresinde production finansal, fiscal, stok ve integration sinyallerini rollback eşikleriyle doğrulamak. |

## 6. Öncelik ve paralellik önerisi

Aşama 1 — V1 uygulama katmanı (V1-EXIT'ten önce):

- `V1-ORD-001`, `V1-ORD-002`, `V1-ORD-003`, `V1-BIL-001/002/003`, `V1-KIT-001..004`,
  `V1-TBL-002/003/004/005`, `V1-CUI-001..003`, `V1-WTR-001..003`, `V1-CSH-001`.
- Paralel başlatılabilirler: `V1-ALT-001`, `V1-OBS-001`, `V1-OPS-001/002`,
  `V1-REC-001`, `V1-RPT-001`, `V1-SET-001`.

Aşama 2 — V1.1 envanter/menü/üretim: `V11-UNT-001`, `V11-INV-001..007`,
`V11-RCP-001/002`, `V11-RSV-001..003`, `V11-MNU-001..003`, `V11-PRD-001/002`,
`V11-PUR-001/002`, ardından UI (`V11-UI-001..003`) ve rapor (`V11-RPT-001`).

Aşama 3 — V1.2 payment/cash/fiscal: `V12-PAY-001..004`, `V12-CSH-001..003`,
`V12-ALC-001..004`, `V12-HUG-001..004` (V0-HUG-001 ön koşul), `V12-MCD-001..004`
(V0-MCD-001 ön koşul), `V12-FSC-001..005` (V0-CMP-001 ön koşul),
`V12-REC-001`, `V12-PUI-001..003`, `V12-TBL-001`, `V12-RPT-001`.

Aşama 4 — V1.3 hesaplar/fatura: `V13-CST-001/002`, `V13-ACC-001..009`,
`V13-INV-001..004`, `V13-QNB-001..005` (V0-QNB-001 ön koşul), `V13-PUR-001`,
`V13-RPT-001`, `V13-UI-001..003`.

Aşama 5 — V1.4 QR/online: `V14-QRS-001..003`, `V14-QRT-001`,
`V14-QRO-001..003` (V0-QRG-001 ön koşul), `V14-CWB-001/002`,
`V14-ONL-001..005`, `V14-MAP-001/002` (V0-YSP-001 ön koşul), `V14-STK-001`,
`V14-REC-001`, `V14-RPT-001`.

Aşama 6 — V1.5 hardening: `V15-SEC-001..003`, `V15-KVK-001/002`,
`V15-BKP-001/002`, `V15-OBS-001..003`, `V15-REC-001/002`,
`V15-PER-001/002`, `V15-SUP-001`, `V15-NOT-001`, `V15-RPT-001`,
`V15-RUN-001/002`.

Aşama 7 — V2.0 sertifikasyon/release: `V20-INS-001/002`, `V20-INT-001..006`,
`V20-DOC-001/002`, `V20-DRL-001`, `V20-MIG-001/002`, `V20-UAT-001..003`,
`V20-SEC-001`, `V20-CMP-001`, `V20-LIC-001/002`, `V20-GAT-001/002`,
`V20-REL-001..005`.

| Kritik dış kanıt | Nerede beklenir | Alınmazsa etki |
| --- | --- | --- |
| Hugin provider contract/erişim | V0-HUG-001 → V12-HUG-001..004 | V12 payment/fiscal gövdesi yazılamaz |
| QNB test tenant/contract | V0-QNB-001 → V13-QNB-001..005 | V13 e-fatura akışı yazılamaz |
| Yemeksepeti Partner erişimi | V0-YSP-001 → V14-ONL/MAP | V14 online sipariş yazılamaz |
| Meal-card provider listesi | V0-MCD-001 → V12-MCD | V12 meal-card + V20-INT-004 bloklu |
| Printer gerçek cihaz kanıtı | V0-PRN-001 → V1-KIT-003/004 | Printed akış kanıtsız |
| Fiscal strateji kararı (T300/QNB) | V0-CMP-001 → V12-FSC-001..005 | Adisyon branch seçilemez |
| License server/sözleşme | V0-LIC-001 → V20-LIC | V20 lisans gate'i boşta |
| İkinci PG18 instance + RPO/RTO | V0-BKP-001/002 → V15-BKP/RUN | Yedek/restore kanıtsız |

## 7. Riskler ve kararlar

1. **Dış kanıt kuyruğu** kritik yolun orta halkasını (V12 fiskal, V13 QNB)
   bekletir: V0-HUG-001, V0-QNB-001, V0-CMP-001 kanıtları dalga 1–3 aralığında
   temin edilmezse dalga 9–11 durur.
2. **V0-SEC-001 deferral notu**: GATES.md `V0-SEC-001`'i "Not V0 gate closure
   evidence" olarak deferral listesinde tutuyor; görev dosyası
   (`plan/v0/security-baseline/V0-SEC-001-security-verification-baseline.md`)
   mevcut ve Done. V14 evresinde gereken şey görev değil, doğrulanmış güvenlik
   gereksinim kaynağı/standart kanıtının toplanmasıdır; V20-SEC-001 bu kanıta
   bağımlı.
3. **UI görevleri** `V0-CMP-005` (erişilebilirlik) ile 20 görevin ortak
   girdisidir; bu contract'ta sapma tüm ekranları etkiler.
4. **NotApplicable yok**: devirlerin hiçbiri dış kanıt yerine onaylı
   `NotApplicable` kararı almamış; kullanıcı supplier'ı reddedemez.
5. Migration her görevde ileri/geri doğrulanır (GATES.md); V11..V20
   pozisyonları sıralı ve fail-closed manifest doğrulamasına tabidir.

## 8. Kapanış ölçütleri

Her dalga, görev bazında `plan_audit_tool.py validate` (0 hata) ve
`task-scope` pre-Done denetimi (allowlist içi write-set) ile kapanır.
Sürüm gate'leri GATES.md koşullarına göre ilerler; `V20-REL-005` Done olduğu
anda tüm zincir kapanmış sayılır.
