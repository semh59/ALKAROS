# Dual-Screen POS Production Topology — approved decision record

> **Task:** V1-RMD-005
> **Status:** Done
> **Source basis:** PO:2026-08-24, V0-ARC-004, V0-ARC-007, V1-RMD-004
> **Access date:** 2026-08-24
> **Approver:** Semih — 2026-08-24
> **Decision type:** Product decision

## Decision

ALKAROS, tek Windows 11 kasa bilgisayarına `Extended desktop` modunda bağlı iki fiziksel ekranı iki bağımsız istemci
olarak çalıştırır:

1. **Cashier client** birincil ekranda kimliği doğrulanmış operatör işlemlerini yürütür.
2. **Customer display client** ikinci ekranda yalnız eşleştiği kasanın müşteriye açık sipariş projection'ını gösterir.

Ekranlar birbirinin belleğini, browser storage alanını veya DOM'unu paylaşmaz. PostgreSQL tek veri otoritesidir. Her
istemci versioned HTTP snapshot alır; SignalR yalnız değişiklik bildirimi taşır. Müşteri ekranının kapanması satış
verisini kaybettirmez, kasa ekranının kapanması müşteri ekranına işlem yetkisi kazandırmaz ve iki ekranın yeniden
başlatılması sunucudaki sipariş durumunu değiştirmez.

```text
Cashier client ── authenticated commands/queries ──┐
                                                   │
Customer display ── read-only snapshot + SignalR ──┼── Local ALKAROS host ── PostgreSQL 18
                                                   │
Windows launcher ── process/display health ────────┘
```

## Runtime and display ownership

- Kasa bilgisayarı Windows 11 x64, desteklenen Chrome sürümü ve iki ayrı browser process'i kullanır.
- Windows görüntü modu `Extend` olmak zorundadır; ekran yansıtma üretim kurulumu olarak kabul edilmez.
- Cashier process birincil ekrana, customer-display process kurulumda kaydedilmiş Windows display device kimliğine
  tam ekran yerleştirilir. Yalnız değişebilen ekran sıra numarası kalıcı kimlik olarak kullanılmaz.
- Customer-display process ayrı browser profili ve ayrı cookie/storage partition'ı kullanır. Cashier session veya
  personel credential'ı bu profile kopyalanmaz.
- Launcher, process ve fiziksel ekran sağlığını izler. Customer-display process kapanırsa kontrollü biçimde yeniden
  başlatır; ikinci ekran ayrılırsa customer view'u birincil ekrana taşımak yerine askıya alır ve cashier client'ta
  görünür uyarı üretir.
- Aynı `TerminalId` için aynı anda tek etkin `DisplayId` bulunur. Yeni eşleşme, operatör onayıyla önceki display
  session'ını iptal eder.

## Authoritative data flow

### Snapshot

Customer display'in tek render girdisi sunucunun ürettiği `CustomerDisplaySnapshot` olur. Snapshot en az şu alanları
taşır:

- `displayId`, `terminalId`, `orderId` ve monoton artan `revision`;
- `state`: `Idle`, `Active`, `Paying`, `Completed` veya `Unavailable`;
- müşteriye açık satır adı, miktar, birim fiyat ve satır toplamı;
- müşteriye açık indirim/ücret satırları, genel toplam, ödenen ve kalan tutar;
- para birimi, server timestamp ve Türkçe müşteri mesajı.

Snapshot yalnız commit edilmiş PostgreSQL durumundan üretilir. Browser belleği, IndexedDB, SignalR mesajı, cashier
DOM'u veya mock service authoritative state değildir. Müşteri ekranı mutation endpoint'i çağırmaz.

### Realtime update

- Commit sonrasında SignalR bildirimi yalnız `displayId`, `orderId`, `revision` ve değişiklik türünü taşır; ürün,
  ödeme veya kimlik verisini event payload'ına kopyalamaz.
- Bildirim alan müşteri ekranı yeni HTTP snapshot'ını çeker. Aynı veya daha eski revision yok sayılır; revision
  boşluğu ve order kimliği değişimi koşulsuz tam snapshot yenilemesi başlatır.
- SignalR kaybını telafi etmek için aktif görünüm en geç beş saniyede bir HTTP snapshot ile reconcile edilir.
- Yeniden bağlantının ilk adımı her zaman tam snapshot'tır. Client, bağlantı kesikken oluşan event'leri tahmin etmez
  veya yerelde yeniden üretmez.
- Sunucu aynı projection değişikliğini tekrar bildirebilir; revision kontrolü render işlemini idempotent yapar.

### Transaction boundary

Sipariş satırı, indirim, ödeme ilerlemesi ve sipariş kapanışı önce ilgili domain transaction'ında commit edilir.
Customer-display projection aynı commit edilmiş gerçeği okur. SignalR bildirimi commit'ten önce yayınlanmaz. Bildirim
başarısız olsa bile periyodik snapshot reconciliation doğru durumu getirir.

## Pairing and authorization

1. Kayıtlı customer display başlarken 128 bit rastgele pairing secret üretir, yalnız process belleğinde tutar ve
   sunucuda iki dakika yaşayan bir pairing request açar.
2. Customer display yalnız kısa, tek kullanımlık eşleşme kodunu gösterir. Kod denemeleri terminal ve kaynak başına
   sınırlandırılır; süresi dolan veya kullanılan kod yeniden kabul edilmez.
3. Kimliği doğrulanmış cashier, doğru `TerminalId` ve fiziksel ekranı görerek eşleşmeyi onaylar.
4. Customer display kendi pairing request'ini poll eder. Onaydan sonra sunucu yalnız bu display'e ait `HttpOnly`,
   `Secure`, `SameSite=Strict` session cookie'si verir; secret ve kısa kod geçersizleşir.
5. Display principal yalnız `customer-display:read` yetkisine, tek `DisplayId` ve tek `TerminalId` claim'ine sahiptir.
   Command, admin, report, identity, fiscal, printer ve payment-provider uçları bu principal için reddedilir.
6. Display kaydı silinince, terminal devre dışı kalınca, yeni display eşleşince veya operatör bağlantıyı kaldırınca
   session derhal iptal edilir. Uzun ömürlü credential URL, localStorage veya log içine yazılmaz.

Pairing ekranı ve bağlantı hatası ekranı hiçbir sipariş verisi göstermez. Başarısız auth durumunda eski snapshot
temizlenir ve client `Unavailable` durumuna geçer.

## Customer-visible data boundary

Customer display şunları gösterebilir:

- işletme adı/logo, kasa veya sipariş için müşteriye anlamlı kısa etiket;
- ürün adı, miktar, birim fiyat, satır toplamı;
- müşteriye yansıyan indirim, ücret, vergi dahil toplam, ödenen ve kalan tutar;
- ödeme yönteminin yalnız genel etiketi (`Nakit`, `Kart` gibi) ve sonuç durumu;
- bağlantı, ödeme bekleme, tamamlanma ve teşekkür mesajları.

Customer display şunları hiçbir durumda göstermez veya istemez:

- personel adı gerekmedikçe personel kimliği, rolü, PIN'i, parola bilgisi veya auth token;
- kart PAN'ı, son dört hane, provider token'ı, authorization code, terminal anahtarı veya callback payload'ı;
- mali cihaz credential'ı, mali belge teknik payload'ı veya printer spool içeriği;
- müşteri telefon/e-posta/adres bilgisi, dahili sipariş notu, audit kaydı veya hata stack trace'i;
- maliyet, kâr, stok, yönetim raporu veya başka terminale ait sipariş.

Sunucu response DTO'su allowlist ile üretilir; genel Order/Payment entity'si doğrudan serialize edilmez.

## Observable states

| Server/client condition | Customer display | Cashier client |
| --- | --- | --- |
| No active order | Markalı `Sıradaki işlem bekleniyor` görünümü | Normal çalışma |
| Order opened or changed | Yeni revision ile satırlar ve toplam | Normal çalışma |
| Payment started | Ödenen ve kalan tutar; provider detayı yok | Payment akışı ve operasyonel detay |
| Order completed | On saniye teşekkür/özet, sonra `Idle` | Sipariş kapanır |
| SignalR disconnected, HTTP healthy | `Bağlantı yenileniyor` göstergesi; beş saniyelik snapshot reconcile | Görünür display health uyarısı |
| HTTP snapshot unavailable | Eski tutarların üstünü kapatan `Bilgi güncellenemiyor` görünümü | Görünür ve sesli olmayan kalıcı uyarı |
| Ten seconds without valid snapshot | Sipariş ve tutarlar temizlenir, `Kasa ekranını takip ediniz` | Display `Stale` alarmı |
| Auth revoked or pairing mismatch | Tüm sipariş verisi temizlenir, pairing ekranı | Yeniden eşleştirme eylemi |
| Customer monitor removed | Process askıya alınır; cashier ekranına taşınmaz | `Müşteri ekranı bağlı değil` alarmı |
| Customer process crash | Launcher restart; snapshot tekrar alınana kadar `Unavailable` | Restart boyunca health alarmı |
| Host or database unavailable | Eski snapshot gizlenir; işlem yapılamaz | Existing fail-closed host davranışı |

Customer display arızası sipariş transaction'ını geri almaz ve cashier client'a gizli bir otomatik başarı üretmez.
Operatör uyarıyı görür; arıza ve iyileşme zamanı health/audit kaydına girer. Release kabulü, müşteri ekranı zorunlu
iken alarm açık durumda satışı başarılı sayamaz.

## Security and privacy controls

- Cashier ve customer display yalnız aynı origin'in TLS korumalı endpoint'lerine bağlanır; CORS ile geniş origin izni
  verilmez.
- Content Security Policy script/style kaynaklarını paketlenmiş origin ile sınırlar; customer display üçüncü taraf
  script, reklam veya analytics yüklemez.
- Snapshot response'u `Cache-Control: no-store` taşır. Service worker, browser HTTP cache ve disk cache sipariş
  snapshot'ını kalıcılaştırmaz.
- Display session, pairing denemesi, auth reddi, stale başlangıcı/sonu, monitor ayrılması ve process restart olayı
  correlation ID ile yapılandırılmış loglanır; token, pairing secret ve sipariş satırı loglanmaz.
- Client hata ekranı teknik ayrıntı vermez. Ayrıntı yalnız yetkili operational log'da bulunur.
- Display principal için server-side authorization zorunludur; UI öğelerini gizlemek güvenlik kontrolü sayılmaz.

## Installation and recovery

- Fresh installer iki browser profilini, launcher kaydını, otomatik başlatmayı ve seçilen ikinci ekran kimliğini aynı
  makinede kurar; müşteri ekranı eşleşmesi işletme sahibi tarafından ilk açılışta yapılır.
- Windows oturum açılışında local host hazır olmadan ekranlar sipariş göstermeye başlamaz. Launcher readiness kontrolü
  geçene kadar iki client da güvenli bekleme ekranında kalır.
- Uygulama veya makine restart'ında sipariş browser storage'dan kurtarılmaz; her iki client sunucudan güncel durumu
  alır. Geçerli display kaydı varsa session güvenli biçimde yenilenir, yoksa pairing gerekir.
- Update/rollback, browser profile ayrımını ve display registration'ı korur; schema/contract uyumsuzluğunda eski
  snapshot render edilmez.
- İki fiziksel ekranlı Windows 11 cihaz testi olmadan installer, UAT veya production deployment görevi `Done` olamaz.

## Operational acceptance scenario

Gerçek donanım kabulünde aynı kasa bilgisayarında şu sıra uygulanır:

1. Windows `Extend` modunda başlatılır; cashier birincil, customer display kayıtlı ikinci ekranda açılır.
2. Cashier oturum açar ve display pairing işlemini onaylar; display başka terminalin siparişini göremez.
3. Sipariş açılır; iki ürün eklenir, biri artırılır, biri silinir ve indirim uygulanır. Her adımda customer display
   server snapshot revision'ı ile doğru satır ve toplamı gösterir.
4. SignalR bağlantısı kesilir; HTTP reconciliation en geç beş saniyede doğru toplamı korur. Ardından host bağlantısı
   kesilir; eski tutarlar en geç on saniyede gizlenir.
5. Bağlantı geri gelir; tam snapshot alınmadan stale uyarısı kalkmaz.
6. Kısmi ödeme ve kalan tutar gösterilir; kart/provider credential'ı görünmez. Sipariş kapanınca teşekkür görünümü on
   saniye sonra temizlenir.
7. Customer process ve ardından bilgisayar yeniden başlatılır; PostgreSQL'deki açık sipariş tekrar görünür, browser
   belleğinden kurgusal sipariş oluşmaz.
8. İkinci ekran kablosu çıkarılır; customer view cashier ekranına taşınmaz ve cashier health alarmı alır. Ekran geri
   bağlanınca kayıtlı device identity ile doğru ekranda yeniden açılır.

Bu senaryo release candidate, exact installer artifact ve iki fiziksel ekran üzerinde kayıt altına alınır.

## Rejected alternatives

1. **Aynı browser tab'leri arasında `BroadcastChannel` veya storage event kullanmak:** process/makine restart'ında
   authoritative değildir, server gerçeğini kanıtlamaz ve iki profil arasında güvenilir değildir.
2. **Cashier ekranını HDMI ile mirror etmek:** müşteri gizliliğini ihlal eder, operatör kontrollerini gösterir ve
   bağımsız müşteri görünümü sağlamaz.
3. **Customer display'e tam cashier session vermek:** least-privilege ilkesini bozar ve ikinci ekrandan mutation
   yapılmasına yol açabilir.
4. **SignalR payload'ını veri otoritesi yapmak:** event kaybı, tekrar ve sıra değişiminde görünümü bozabilir; versioned
   HTTP snapshot seçildi.
5. **Snapshot'ı IndexedDB'de kalıcı tutup offline toplam göstermek:** müşteri yanlış/eski tutarı gerçek sanabilir;
   bağlantı kaybında fail-closed stale görünümü seçildi.
6. **Customer display arızasında satışı sessizce sürdürmek:** operasyonel hata görünmez kalır; cashier health alarmı ve
   audit kaydı zorunlu seçildi.
7. **Monitörü yalnız Windows sıra numarasıyla seçmek:** kablo/driver değişiminde yanlış ekrana taşınabilir; kalıcı
   display device identity ve yeniden doğrulama seçildi.

## Affected tasks and custody

- `V1-RMD-004`: Mock kalır; production backend, iki-screen synchronization veya go-live kanıtı sayılmaz.
- `V1-CUI-001`, `V1-CUI-002`, `V1-CUI-003`: Tarihsel cashier davranışları yeniden açılmaz; yeni entegrasyon bunların
  kontratlarını tüketir.
- `V1-ORD-002`, `V1-IAM-003`, `V1-SEC-002`, `V1-OBS-001`: Sipariş otoritesi, device session, sensitive payload ve
  health/log sınırları yeni production implementation görevlerine girdi olur.
- `V20-INS-001`: İki process, ayrı browser profili, display placement, startup ve watchdog kurulumu kanıtlanır.
- `V20-SEC-001`: Display principal, pairing, session revocation, cache, CSP ve veri minimizasyonu bağımsız incelenir.
- `V20-UAT-001`: Yukarıdaki iki fiziksel ekran senaryosu exact release candidate üzerinde yürütülür.
- `V20-REL-002`, `V20-REL-003`, `V20-REL-004`: Sentetik pilot, signed go-live ve production deployment bu kararı ve
  gerçek donanım kanıtını tüketir.

Mevcut planda customer-display server projection, read-only client ve cashier/display integration yüzeylerini sahiplenen
uygulama görevleri yoktur. Bu karar production davranışı tamamlanmış saymaz. Bu üç sorumluluk ve composition/build
entegrasyonu, çakışmasız exact owned surface'lerle ayrı plan değişikliğinde kabul edilmeden uygulama kodu yazılamaz.

## Release boundary

Bu karar production izni değildir. Gerçek müşteri veya gerçek para ile kullanım yalnız `V20-REL-003` signed
`Approve` ve ardından `V20-REL-004` kanıtlı deployment ile mümkündür. Payment, fiscal device ve printer davranışları
kendi gerçek contract/device kanıtları olmadan customer display veya cashier client içinde başarı olarak gösterilemez.
