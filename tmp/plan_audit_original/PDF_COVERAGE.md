# PDF Coverage Matrix

Bu matris, `PDF_SOURCE.md` ile kimliği sabitlenen 94 sayfalık master PDF'nin plan
içinde sahipsiz kalmamasını sağlar. Bir satırın burada bulunması görevin
tamamlandığı anlamına gelmez; bütün görevler gerçek kabul kanıtı görülene kadar
`Planned`, `InProgress` veya `Blocked` kalır.

## Part I - Versioned Roadmap

| PDF section | Kapsam | Plan owner |
|---|---|---|
| I.0-I.5 | Ürün hedefi, sabit kararlar, English code/Turkish UI, domain haritası, sürüm modeli | V0-ARC-001, V0-ARC-004, V0-DOM-001 |
| I.6 | Hugin, meal card, QNB, Yemeksepeti, QR relay, printer feasibility | V0-HUG-001, V0-MCD-001, V0-QNB-001, V0-YSP-001, V0-QRG-001, V0-PRN-001 |
| I.7-I.10 | Core restaurant, Order, table transfer ve merge | V1-FND-001, V1-IAM-001, V1-CAT-001, V1-TBL-001, V1-TBL-002, V1-TBL-003, V1-ORD-001 |
| I.11-I.15 | Split bill, Bill/Payment/Allocation, closure, concurrency ve idempotency | V0-DOM-002, V0-DOM-004, V1-BIL-001, V1-BIL-002, V1-FND-002, V12-PAY-001, V12-ALC-001 |
| I.16-I.20 | Kitchen, printing, reconciliation, alerting ve backup foundation | V1-KIT-001, V1-KIT-002, V1-KIT-003, V1-KIT-004, V1-REC-001, V1-ALT-001, V1-OPS-002 |
| I.21-I.25 | Menu, recipe, production, portion inventory/reservation ve shared pool | V11-MNU-001, V11-MNU-002, V11-RCP-001, V11-RCP-002, V11-UNT-001, V11-PRD-001, V11-PRD-002, V11-RSV-001, V11-RSV-002, V11-RSV-003 |
| I.26-I.29 | Payment, Hugin, fiscal davranış ve meal card | V12-PAY-001, V12-PAY-002, V12-ALC-001, V12-ALC-002, V12-ALC-003, V12-HUG-001, V12-HUG-002, V12-HUG-003, V12-HUG-004, V12-FSC-001, V12-FSC-002, V12-FSC-003, V12-MCD-001, V12-MCD-002, V12-MCD-003 |
| I.30-I.33 | Customer account, periodic invoicing, QNB, incoming invoice ve KVKK | V13-CST-001, V13-CST-002, V13-ACC-001, V13-ACC-002, V13-ACC-003, V13-ACC-004, V13-INV-001, V13-INV-002, V13-INV-003, V13-INV-004, V13-QNB-001, V13-QNB-002, V13-QNB-003, V13-QNB-004, V13-QNB-005, V0-CMP-003 |
| I.34-I.37 | QR security/order confirmation, online ordering ve cross-channel stock race | V14-QRS-001, V14-QRS-002, V14-QRS-003, V14-QRO-001, V14-QRO-002, V14-QRO-003, V14-ONL-001, V14-ONL-002, V14-ONL-003, V14-ONL-004, V14-ONL-005, V14-MAP-001, V14-MAP-002, V14-STK-001 |
| I.38-I.44 | Hardening, full reconciliation, alerts, backup/restore, observability, security ve cash session | V15-REC-001, V15-REC-002, V15-NOT-001, V15-BKP-001, V15-BKP-002, V15-OBS-001, V15-OBS-002, V15-OBS-003, V15-SEC-001, V15-SEC-002, V15-SEC-003, V1-CSH-001, V12-CSH-001, V12-CSH-002 |
| I.45-I.54 | Production kriterleri, final lifecycle/invariant, edge cases, geliştirme sırası ve consistency gates | V20-GAT-001, V20-GAT-002, V20-MIG-001, V20-MIG-002, V20-SEC-001, V20-CMP-001, V20-DRL-001, V20-UAT-001, V20-UAT-002, V20-UAT-003, V20-REL-001, V20-REL-002, V20-REL-003 |

## Part II ve Part III - Domain/Schema Ownership

Part II'nin bounded context ve kural alanları ile Part III'ün şema grupları aynı
satırda eşleştirilmiştir. Şema satırının varlığı, PDF'deki tablo tasarımının
aynen uygulanacağı anlamına gelmez; C1-C9 ve ek denetim düzeltmeleri önce ilgili
V0 contract görevlerinde kapanır.

| PDF domain/schema | Tekil plan sahibi veya görev ailesi |
|---|---|
| II.0-II.1, III.0-III.2 - Genel ilkeler ve shared conventions | V0-CMP-002, V0-DAT-001, V0-DAT-002, V0-DAT-003, V0-DAT-004, V0-ARC-001, V0-ARC-004 |
| II.2.1, III.3 - Identity & Authorization | V1-IAM-001, V1-IAM-002, V1-IAM-003 |
| II.2.2, III.4 - Catalog | V1-CAT-001, V1-CAT-002 |
| II.2.3, II.3.16, II.5.15, III.5 - Table Management | V0-DOM-005, V1-TBL-001, V1-TBL-002, V1-TBL-003, V1-TBL-004, V1-TBL-005 |
| II.2.4, II.3.2, II.5.1, III.6 - Order | V0-DOM-001, V1-ORD-001, V1-ORD-002, V1-ORD-003 |
| II.2.5, II.3.3, II.5.2, III.7 - Bill | V0-DOM-002, V0-DOM-006, V1-BIL-001, V1-BIL-002, V1-BIL-003 |
| II.2.6, II.3.4-II.3.5, II.5.3, III.8 - Payment/Allocation | V0-DOM-003, V0-DOM-004, V12-PAY-001, V12-PAY-002, V12-ALC-001, V12-ALC-002, V12-ALC-003 |
| II.2.7, II.5.9, III.9 - Cash | V1-CSH-001, V12-CSH-001, V12-CSH-002 |
| II.2.8-II.2.9, II.3.6, III.10-III.11 - Menu/Daily Menu | V11-MNU-001, V11-MNU-002, V11-MNU-003 |
| II.2.10, II.3.7, III.12 - Recipe/Units/Cost | V11-UNT-001, V11-RCP-001, V11-RCP-002 |
| II.2.11, II.3.8, II.5.5, III.13 - Production | V11-PRD-001, V11-PRD-002 |
| II.2.12, II.3.9, II.5.6/II.5.14, III.14 - Inventory/Reservation | V11-INV-001, V11-INV-002, V11-INV-003, V11-INV-004, V11-INV-005, V11-INV-006, V11-RSV-001, V11-RSV-002, V11-RSV-003 |
| III.15 - Purchasing | V11-PUR-001, V11-PUR-002, V13-PUR-001 |
| II.2.13, II.3.13-II.3.14, II.5.7-II.5.8, II.8, III.16 - Kitchen/Print | V1-KIT-001, V1-KIT-002, V1-KIT-003, V1-KIT-004 |
| II.2.14, II.3.10, II.5.10, III.17 - Meal Card | V0-MCD-001, V12-MCD-001, V12-MCD-002, V12-MCD-003 |
| II.2.15, II.3.11, III.18 - Customer Account | V0-DOM-007, V13-CST-001, V13-CST-002, V13-ACC-001, V13-ACC-002, V13-ACC-003, V13-ACC-004 |
| II.2.16, II.3.12, II.5.4, III.19 - Fiscal | V0-CMP-001, V12-FSC-001, V12-FSC-002, V12-FSC-003, V12-HUG-001, V12-HUG-002, V12-HUG-003, V12-HUG-004 |
| II.2.17, II.5.11, III.20 - Invoicing | V13-INV-001, V13-INV-002, V13-INV-003, V13-INV-004, V13-QNB-001, V13-QNB-002, V13-QNB-003, V13-QNB-004, V13-QNB-005 |
| II.2.18, II.6.8, II.7.3, III.21 - QR Ordering | V14-QRS-001, V14-QRS-002, V14-QRS-003, V14-QRO-001, V14-QRO-002, V14-QRO-003, V14-CWB-001, V14-CWB-002 |
| II.2.19, II.7.4, III.22 - Online Ordering | V14-ONL-001, V14-ONL-002, V14-ONL-003, V14-ONL-004, V14-ONL-005, V14-MAP-001, V14-MAP-002, V14-OUI-001, V14-STK-001 |
| II.2.20, II.10, III.31 - Reporting | V0-DOM-008, V1-RPT-001, V11-RPT-001, V12-RPT-001, V13-RPT-001, V14-RPT-001, V15-RPT-001 |
| II.2.21, II.3.15, II.5.12, II.6.11, III.23 - Reconciliation | V1-REC-001, V12-REC-001, V14-REC-001, V15-REC-001, V15-REC-002 |
| II.2.22, II.9, III.24 - Audit | V1-OPS-001 |
| II.2.23, III.25 - Backup | V0-BKP-001, V0-BKP-002, V1-OPS-002, V15-BKP-001, V15-BKP-002, V20-DRL-001 |
| II.2.24, III.26 - Licensing | V0-LIC-001, V20-LIC-001, V20-LIC-002 |
| III.27 - Settings | V0-ARC-005, V1-SET-001 |
| II.2.25, II.5.13, III.28 - Observability/Alert | V1-OBS-001, V1-ALT-001, V15-OBS-001, V15-OBS-002, V15-OBS-003, V15-NOT-001 |
| II.11-II.12, III.33-III.34 - Security/KVKK/lifecycle | V0-CMP-003, V15-SEC-001, V15-SEC-002, V15-SEC-003, V15-KVK-001, V15-KVK-002, V20-SEC-001, V20-CMP-001 |
| II.13-II.15, III.29-III.40 - Version summary, constraints, indexes, migrations ve final gates | V0-DAT-001, V0-DAT-002, V0-DAT-003, V0-DAT-004, V20-MIG-001, V20-MIG-002, V20-GAT-001, V20-GAT-002 |

## Part IV - C1-C9 Düzeltme Sahipleri

| Finding | PDF'de kanıtlanan sorun | Plan düzeltmesi |
|---|---|---|
| C1 | `table_mgmt.tables` içindeki forward/circular FK'ler migration sırasını kırıyor | V0-DAT-001 iki geçişli migration/FK graph kararı; V20-MIG-001 ve V20-MIG-002 gerçek rehearsal |
| C2 | `NotReserved`, Draft/Submitted/PendingConfirmation ayrımını açıklamıyor | V0-DAT-002 kanonik anlamı bağlar; V11-RSV-001 ve V14-QRO-001 davranışı test eder |
| C3 | `account_transactions.amount` sign convention belirsiz | V0-DOM-007 tek balance formülü ve Adjustment istisnasını bağlar; V13-ACC-001 uygular |
| C4 | Internal allocation için idempotency key üretimi tanımsız | V0-DOM-004 scope'u bağlar; V1-FND-002 ve V12-ALC-001 double-submit/retry davranışını uygular |
| C5 | QR PendingConfirmation sırasında masa state'i seating race yaratıyor | V0-DOM-005 geçişi bağlar; V14-QRO-002 Reserved/Occupied/Available concurrency kuralını uygular |
| C6 | Meal-card settlement parent/child status'u atomik değilse drift oluşuyor | V0-DAT-004 projection ownership; V12-MCD-002 tek transaction ve rebuild/mismatch kanıtı |
| C7 | Polymorphic discriminator alanlarının kanonik değerleri yok | V0-DAT-002 tüm internal discriminator değerlerini tek katalogda kapatır |
| C8 | I.46 metni hâlâ `14` yazıyor; Part IV doğru başlangıç sayısını `13` veriyor | V0-DOC-001 kaynak sayımını ve tüm cross-reference düzeltmelerini doğrular |
| C9 | `waste_factor` ile unit conversion işlem sırası tanımsız | V11-PRD-002 native-unit waste ardından tracked-unit conversion sırasını uygular ve test eder |

## PDF Dışında Uydurulmadan Kapatılacak Açıklar

| Açık veya eksik sözleşme | Plan davranışı |
|---|---|
| e-Adisyon ifadesi PDF'de yok; güncel GİB uygulanabilirliği kanıtlanmamış | V0-CMP-001 resmî kapsamı doğrular; V12-FSC-003 yalnız onaylı stratejiyi uygular |
| Bill ile birden çok Order ilişkisinin kardinalitesi açık değil | V0-DOM-002 bağlayıcı aggregate/cardinality kararı olmadan Bill migration başlamaz |
| Refund/reversal ledger ve allocation ters kayıtları eksik | V0-DOM-003 kararından sonra V12-PAY-002 ve V12-ALC-003 uygulanır |
| Nullable unique alanların bir kısmı `NULL` tekrarını engellemiyor | V0-DAT-003 PostgreSQL partial/expression unique politikasını bağlar |
| Cached totals ve current pointers için source-of-truth/rebuild sahipliği dağınık | V0-DAT-004 tamamlanmadan ilgili projection görevi başlamaz |
| Table reservation persistence ve QR rezervasyon davranışı aynı şey değil | V0-DOM-005, V1-TBL-004 ve V14-QRO-002 ayrı sahiplikte tutulur |
| Discount, complimentary, service fee ve tip uygulanabilirliği tam bağlanmamış | V0-DOM-006 ve V0-CMP-004 kararları olmadan V1-BIL-003 uygulanmaz |
| Supplier master var ama supplier account/borç etkisi eksik | V11-PUR-002 master'ı, V13-PUR-001 finansal etkileri sahiplenir |
| RPO/RTO değerleri başlık olarak var fakat ölçülebilir hedef kararı gerektiriyor | V0-BKP-002 hedefleri bağlar; V20-DRL-001 ölçer |
| Licensing davranışı şemada var fakat çalışma sözleşmesi eksik | V0-LIC-001 karar verir; V20-LIC-001 uydurma server/telemetry olmadan uygular veya onaylı N/A üretir |
| Online catalog ve availability outbound akışları şemadan tek başına çıkmıyor | V14-ONL-004 ve V14-ONL-005 provider capability/sandbox kanıtıyla ayrı uygulanır |
| UI katmanları PDF'deki kullanıcı akışlarını çalıştırılabilir işe çevirmiyor | V1-CUI/WTR, V11-UI, V12-PUI, V13-UI, V14-CWB/OUI görevleri domain contract'larından ayrı sahiplenilir |

## Coverage kapısı

V20-GAT-001 aşağıdaki üç durumu sıfırdan farklı bulursa release gate kapanmaz:

1. PDF bölümü var, plan owner yok.
2. Plan owner var, acceptance evidence yok.
3. PDF belirsiz veya çelişkili, fakat onaylı decision/validation task yok.
