# V1-RMD-393 — Para akışı hedefli derin denetim raporu

- Tarih: 2026-09-28
- Denetlenen commit: `cc25c0f` (master HEAD) + bu görevin plan/kanıt commit'leri
- Yürütücü: claude-code-session_012a6DqV4367gGp1Uk8TUam1
- Kapsam: sipariş → hesap → bölme → indirim/bahşiş → tahsilat (nakit/EFT/kart) → dağıtım (allocation) →
  hesap kapanışı → kasa oturumu → gün sonu → ödeme mutabakat raporu
- Üretim kodu, test projesi, migration veya başka görev dosyası **değiştirilmedi**.

## 0. Yüzeysel olmaması için uygulanan yöntem

| Önlem | Ne yapıldı | Nerede |
| --- | --- | --- |
| Çalıştırılmış kanıt | Her iddia, gerçek PostgreSQL 18 + gerçek `DualScreenApplication` host üzerinde koşan bir probe testine bağlandı. Kod okuması tek başına bulgu sayılmadı; yalnız okumaya dayananlar ayrıca işaretlendi. | `probes/` |
| Doğru davranışı doğrulayan testler | Probe'lar "sistem ne yapmalı" sorusunu doğrular: başarısız probe = bulgu, geçen probe = sağlam hücre. | `probes/MoneyFlowProbes/MoneyFlowProbes.cs` |
| Kontrol probe'u | Mutlu yol probe'u (P10) geçmek zorundaydı; geçti. Harness'in her şeyi kırmızı göstermediği kanıtlandı. | P10 |
| Referans koşusu | Tam `dotnet test ALKAROS.slnx` koşuldu; master'ın gerçek durumu ölçüldü. | §2 |
| Mutasyon denetimi | Var olan korumalar tek tek bozuldu; mevcut testlerin bunu yakalayıp yakalamadığı ölçüldü. | §5, `mutation/` |
| Kör kalibrasyon | Denetimden bağımsız bir ajan para akışına gizli bir hata yerleştirdi; açıklama SHA-256 ile mühürlendi, probe'lar koşulduktan **sonra** açıldı. | §6, `calibration/` |
| Bağımsız çürütme | Bulgular, bağlamı bilmeyen ikinci bir ajan tarafından çürütülmeye çalışıldı. | §7 |
| Çürütülen hipotezler | Kendi yanlış hipotezlerim de kayda geçti. | §4 |

## 1. Invariant listesi

| Kod | Invariant |
| --- | --- |
| INV-1 | Bir hesabın dağıtılmış toplamı hiçbir anda indirim/bahşiş sonrası ödenecek tutarı aşamaz (V0-DOM-004). |
| INV-2 | Hesap yalnız onaylı dağıtımlar düzeltilmiş ödenecek tutarı karşıladığında ve çözülmemiş ödeme yokken `Paid` olur; karşılandığında da mutlaka `Paid` olabilmelidir. |
| INV-3 | Çözülmemiş (Unknown/Pending/ReconciliationRequired) bir ödeme varken hiçbir yöntemle yeni tahsilat eklenemez (V1-RMD-258). |
| INV-4 | Bir idempotency anahtarı tek bir komutu adlandırır; tekrar = aynı sonuç, farklı komut = ret. Eşzamanlı tekrar tek kayıt üretir. |
| INV-5 | Kasa beklenen tutarı = açılış + nakit satış (onaylı tutar; para üstü hariç) ± kasa hareketleri. |
| INV-6 | Kasa oturumu tek terminale aittir; mutabakat yalnız Supervisor/Accountant/Admin (cash-session-design.md:109). |
| INV-7 | İptal edilmiş hesaba para alınamaz. |
| INV-8 | Raporlanan günlük ciro / yöntem kırılımı, kayıtlı onaylı ödemelerden türetilir. |
| INV-9 | Kullanıcıya gösterilen ödenecek tutar, sunucunun uyguladığı tutarla aynıdır. |

## 2. Referans koşusu ve süreç bulguları

### F-01 — CI 26 Eylül 08:06'dan beri hiç yeşil değil; o saatten sonraki hiçbir değişiklik doğrulanmadı (Kritik)

- Son yeşil `production-validation` koşusu: run #264, 2026-09-26T08:06Z.
- Son 100 koşunun 100'ü kırmızı. #265–#276: `markdownlint` adımında düşüyor, .NET testlerine hiç gelmiyor
  (örnek run 36244147270: `V12-RMD-005 ... MD036`, `V1-RMD-316 ... MD032`).
- #277'den (2026-09-26T13:39Z) beri üç job da 3–5 saniyede, **hiçbir adım çalışmadan** düşüyor ve log
  üretmiyor (API: HTTP 404). Bu, GitHub'ın job'ı başlatmadığını gösterir (hesap/limit seviyesi); koddan
  bağımsız bir durum, deponun içinden doğrulanamadı.
- Sonuç: 26 Eylül denetiminin bütün düzeltmeleri (V1-RMD-310…339), UI denetim turları (…392) ve V14 işleri
  CI'dan geçmeden master'a girdi.

### F-02 — Kilitli restore master HEAD'de bozuk: Docker imajı ve CI restore adımı derlenemiyor (Kritik)

- `dotnet restore ALKAROS.slnx --locked-mode` 39 test projesinde `NU1004` ile düşüyor: Host'a eklenen
  `ALKAROS.CustomerData` / `ALKAROS.CustomerAccounts` referansları test projelerinin `packages.lock.json`
  dosyalarına yansımamış.
- Kaynak: `e8357e3` (V14-CST-001) ve `674337e` (V14-ACC-001).
- `deploy/docker/Dockerfile:19` ve `.github/workflows/task-scope.yml:200` bu komutu kullanıyor; yani CI
  çalışsaydı da, Docker imajı da bugün derlenemez.
- Tekrar: temiz bir klonda `dotnet restore ALKAROS.slnx --locked-mode` → çıkış kodu 1, 39 projede 116
  `NU1004` hatası (`baseline/restore-locked.log`).

### F-03 — Master'da üç test paketi kırmızı; üç görev yeşil koşu olmadan `Done` (Yüksek)

Kilitsiz restore ile yapılan tam referans koşusu (`dotnet test ALKAROS.slnx -c Release`): 147 test projesinden
143'ü geçti, 4'ü başarısız; **3.335 test geçti, 5 test başarısız** (`baseline/full-suite-summary.txt`).

| Paket | Sebep | Kaynak commit |
| --- | --- | --- |
| Host.Experience.Production (2 test) | `ProductionStockEffectService` → `IUnitConverter` Production deneyiminde kayıtlı değil; tam host'ta başka deneyimler kaydettiği için üretimde çalışıyor, ama deneyim kendi bağımlılığını taşımıyor. | `0fafa58` V1-RMD-344 (09-27) |
| Host.Experience.Composition (1 test) | Trendyol consumer'ı factory ile kaydedildi (`ImplementationType == null`); test `GetType() == ImplementationType` bekliyor. Çalışma zamanı sorunu değil, test kırık. | `6b29f93` V12-TGO-003 (09-27) |
| CustomerData.AnonymizationState (1 test) | `DateTimeOffset` 100 ns, PostgreSQL 1 µs hassasiyet; test tam eşitlik bekliyor, çoğu koşuda düşer. | `bf4205e` V14-CST-002 (09-28) |
| Host.Experience.SecurityAdministration (1 test) | Denetim ortamındaki `pg_dump` 16 / sunucu 18 uyumsuzluğu. **Ortam kaynaklı, bulgu değil.** | — |

## 3. Bulgular (probe ile doğrulandı)

Önem: Kritik > Yüksek > Orta > Düşük. "Doğrulayıcı" sütunu §7'deki bağımsız çürütme sonucudur.

| Kod | Probe | Özet | Önem | Doğrulayıcı |
| --- | --- | --- | --- | --- |
| F-04 | P01 | Kart ödemesi çözülmemişken (Unknown) nakit tahsilat kabul ediliyor; EFT ve kart bu durumu reddediyor. Kart gerçekte çekildiyse müşteri iki kez ödemiş olur. | Yüksek | KISMİ: Cash'in dışarıda bırakılması V1-RMD-258:142-147'de bilinçli; gerekçe ("nakit kendisi Unknown olmaz") başka bir ödemenin Unknown olmasını kapsamıyor. Arayüzde yalnız tarayıcı kilidi var (`split-payment.js:741-756`). |
| F-05 | P02, P12 | Kısmi ödemeden sonra uygulanan indirim, ödenecek tutarı tahsil edilenin altına düşürebiliyor (80 alındı, 30 indirim → ödenecek 70, kalan −10). Fazla tahsilat hiçbir yerde kaydedilmiyor. | Yüksek | DOĞRULANDI; normal arayüzden erişilebilir. |
| F-06 | P09 | Kapanışı engelleyen çözülmemiş kart denemesi "kart çekilmedi" diye çözülünce hesap kapatılmıyor; kalan 0, durum `Open`. Arayüzden kapatma yolu yok (aynı nakit anahtarıyla elle yeniden istek kapanışı tetikler). Yalnız F-04 üzerinden erişilir. | Orta | KISMİ: "hiç uç nokta yok" iddiası düzeltildi (`CashSession.cs:258` tekrar isteğinde de kapanışı dener). |
| F-07 | P03 | İptal edilmiş hesaba nakit tahsilat kabul ediliyor. Üretimde recall akışı hesabı iptal eder; ekranında eski hesap açık kalan kasiyer ödeme alabilir. Para ölü hesapta kalır (`TryClose` → `NotClosable`), yeniden gönderilen hesap yeni ödeme ister. | Yüksek | DOĞRULANDI; önem Orta'dan Yüksek'e çıkarıldı. |
| F-08 | P04 | B terminalinde oturum açmış kasiyer, A terminalinin kasa oturumuna satış yazabiliyor (oturumun terminale ait olduğu kontrol edilmiyor). Aynı açık `/close`, `/reconcile`, `/counts`, `/cash-movements` için de geçerli (kodda okundu). | Orta | DOĞRULANDI; içeriden (IDOR) risk, oturum kimliği yalnız kendi terminaline açık. |
| F-09 | P05 | Kasiyer kendi kasa oturumunu mutabakatlayabiliyor; spec yalnız Supervisor/Accountant/Admin diyor. | Orta | KISMİ: V13-CSH-004:45-49 izni bilinçli eklemedi, CashSession'ın "kendi rol modeline" dayandı; o rol modeli kodda yok. |
| F-10 | P06 | Gün sonu kapanışı ciro ve sipariş sayısını istemcinin gönderdiği gövdeden olduğu gibi saklıyor; ödemelerden türetmiyor. İstemcilerde bu uç noktayı çağıran kod yok. | Düşük–Orta | KISMİ: V1-RMD-249:57-61'de belgelenmiş sözleşme; yalnız `reports.close-day` (yönetici) çağırabilir. Para hareketi değil, rapor bütünlüğü açığı. |
| F-11 | P14 | İndirimden sonra bölme tasarımı indirimsiz tutarı (100) gösteriyor, sunucu indirimli tutarı (90) uyguluyor; arayüzün zorunlu kıldığı toplamla kayıt 400 dönüyor. İndirimli hesapta tutar bazlı bölme arayüzden yapılamıyor. | Orta | DOĞRULANDI. |
| F-12 | P08 | EFT'de kullanılmış idempotency anahtarıyla gelen nakit tahsilat 200 "tekrar" olarak dönüyor ama kasaya satış kaydı yazılmıyor. | Düşük | KISMİ: istemci yöntem/tutar değişince anahtarı sıfırlıyor (`split-payment.js:234,787,808`); yalnız elle hazırlanmış istekle erişilir. |

Tekrarlama senaryoları (Semih'in elle deneyebileceği):

- **F-04:** 100 TL hesap → "Banka kartı" ile öde (sonuç: mutabakat gerekiyor) → aynı hesap için kasa
  ekranından 100 TL nakit al → kabul edilir.
- **F-05:** 100 TL hesap → 80 TL EFT → 30 TL sabit indirim uygula → özet ekranında kalan −10 görünür.
- **F-06:** F-04 adımlarının ardından yönetici "kart çekilmedi" der → kalan 0 olduğu halde hesap açık kalır, masa/sipariş tamamlanmaz.
- **F-11:** 100 TL hesaba %10 indirim → Bölme ekranında "Kişi tutarları" → toplam 100 (ekranın istediği) →
  Kaydet → sunucu reddeder.

## 3b. Bulgular (yalnız kod okuması; çalışma zamanında erişilemiyor veya probe'lanmadı)

| Kod | Özet | Önem |
| --- | --- | --- |
| F-13 | `AlwaysApproveCreditPolicy` (hiçbir cari borçlandırmayı reddetmez) ve `NoKnownBlockingReferencesGuard` (hiçbir KVKK anonimleştirmesini engellemez) üretim modül kataloğunda kayıtlı (`ModuleRegistry.cs:27,62`). AGENTS.md:52,60'taki placeholder/mock-success yasağına aykırı; buna karşın V14-ACC-003:51-52,64 ve V14-CST-002:49-52 bunları "dürüst yer tutucu" olarak onaylamış. Bugün hiçbir Host/istemci çağırmıyor; ilk anonimleştirme veya cariye yazma uç noktası açıldığında Yüksek olur. Guard'ın gerekçesi ("V14-ACC Planned") artık geçersiz: V14-ACC-001…003 Done. | Düşük (bugün) |
| F-14 | Kasa açılışında `Opening` defter kaydı oturum kaydından ayrı, atomik olmayan bir yazma (`DualScreenApplication.CashSession.cs:76-86`); ikinci yazma düşerse beklenen kasa 0'dan başlar. | Düşük |
| F-15 | Spec sapmaları: kapanış `Open` durumdan da yapılabiliyor (spec: Counting/Closing), sayım `Open` durumda da kaydedilebiliyor (spec: Counting) — `CashSessionPolicy.cs`. | Düşük |
| F-16 | Recall akışında para kontrolü ile hesap iptali aynı transaction'da değil (`OrderManagementEndpoints.cs:398-405`): kontrol ile iptal arasına giren bir tahsilat iptal edilmiş hesaba bağlı kalır. Dar yarış penceresi. | Düşük |

## 4. Çürütülen hipotezler (bulgu değil)

| Hipotez | Neden çürüdü |
| --- | --- |
| Recall akışı kısmen ödenmiş hesabı iptal ediyor | `CashierHandoffStore.cs:116-129` tahsilat, `Paid` ya da çözülmemiş ödeme varsa `CheckHasPaymentException` atıyor. (Yarış penceresi F-16 olarak ayrıca kaydedildi.) |
| Üretim partisi tamamlama üretimde 500 veriyor | Tam host'ta `IUnitConverter` başka deneyimlerce kaydediliyor; hata yalnız Production deneyiminin kendi test host'unda (F-03). |
| Composition testi gerçek bir DI hatası gösteriyor | Factory kaydının `ImplementationType`'ı `null`; çalışma zamanında consumer çözülüyor (F-03). |
| Bölme motoru hâlâ ham `PayableAmount` kullanıyor (26 Eylül K1) | `SplitEngine.cs:32,102,298` düzeltilmiş tutarı kullanıyor; sorun yalnız DTO'da (F-11). |
| P12'deki aşırı tahsilat bir kilit yarışıdır | Başarısız iterasyonlar EFT'nin önce çalıştığı sıralamalar; kök neden F-05 ile aynı, ayrı bulgu sayılmadı. |

## 5. Mutasyon denetimi

Her mutasyon ayrı, atılabilir bir klonda bir korumayı bozdu; o korumanın sahibi olan modül testleri ve aynı
akışın Host HTTP testleri koşuldu (`mutation/run-mutations.sh`, çıktı `mutation/results.log`).

| Kod | Bozulan koruma | Sonuç |
| --- | --- | --- |
| M01 | EFT: çözülmemiş ödeme kilidi | Yakalandı (Host PaymentTender) |
| M02 | Nakit: idempotency anahtarı kilidi | Yakalandı (Cash.TenderHandler) |
| M03 | Kapanış: onaysız ödemelerin dağıtımını saymak | Yakalandı (Billing.PaymentClosure) |
| M04 | Beklenen kasa: sayım düzeltme/kapanış farkını da saymak | **Hayatta kaldı** — ama `CountAdjustment`/`ClosingDifference` üretimde hiçbir kod tarafından yazılmıyor; bugün gözlemlenemeyen yol. |
| M05 | Dağıtım tavanı (V0-DOM-004) | Yakalandı (Allocations.Persistence) |
| M06 | Düzeltilmiş ödenecek tutarda bahşişi yok saymak | Yakalandı (Billing.Adjustments, Host Billing) |
| M07 | Kapanış: Pending/Unknown engelleyicilerini yok saymak | **Hayatta kaldı — gerçek test boşluğu.** "Tam karşılanmış ama çözülmemiş kart ödemesi olan hesap açık kalır" hiçbir testte yok; F-04'te nakit tam bu korumanın arkasından geçiyor. |
| M08 | Nakit: para üstünü sıfırlamak | Yakalandı (Cash.TenderHandler, Host CashSession) |
| M09 | Kart: çözülmemiş denemenin üstüne yeni deneme | Yakalandı (Host PaymentTender) |
| M10 | Elle kart onayında dört göz ilkesi | Yakalandı (Host PaymentTender) |

Özet: 10 mutasyonun 8'i yakalandı. Mevcut korumaların testleri genel olarak güçlü; boşluklar korumaların
**olmadığı** yerlerde (F-04…F-11) ve kardinalitede (§6).

## 6. Kör kalibrasyon

- Bağımsız bir ajan, para akışına tek satırlık gerçekçi bir hata yerleştiren bir yama ve mühürlü bir açıklama
  üretti. İkisinin SHA-256'sı 06:58:33Z'de, probe'lar yazılmadan önce kaydedildi (`calibration/SEAL.sha256`).
- Probe'lar yamasız ve yamalı iki kopyada koşuldu; sonuçlar **birebir aynı** çıktı
  (`calibration/run-seeded-blind.log`). Karar, mühür açılmadan önce yazıldı
  (`calibration/VERDICT-before-unseal.txt`).
- **Sonuç: KAÇIRILDI.** Hata `AdjustmentCalculator.cs:42`'de `tipGross +=` yerine `tipGross =`: bir hesapta
  birden fazla bahşiş varsa yalnız sonuncusu sayılıyor.
- **Neden kaçtı:** Kapsam matrisinde "kardinalite" boyutu yoktu; her probe en fazla bir indirim ve bir bahşiş
  kullanıyordu.
- **Mevcut test paketi de yakalamıyor:** Yamalı kopyada Billing/Cash/EFT/Allocation/Host para akışı test
  projelerinin tamamı yeşil (`calibration/existing-suite-on-seeded.log`).
- Düzeltme: kardinalite probe'u P15 eklendi. Yamasız kopyada geçiyor, yamalı kopyada düşüyor (198 beklenen,
  173 gerçek). **Bu ikinci koşu kör değildir**; yalnız açığın kapandığını gösterir.

## 7. Bağımsız çürütme

Bağlamı bilmeyen ikinci bir ajana yalnız iddialar (gerekçe değil) verildi; her birini (a) belgelenmiş bilinçli
karar, (b) üretimde erişilemeyen kurulum, (c) kodun yanlış okunması açısından çürütmesi istendi. Probe'ları
`alk-sdk` konteynerinde kendisi yeniden koştu ve aynı sonucu aldı (10 başarısız, 5 geçen).

- **Tamamen çürütülen iddia yok.**
- DOĞRULANDI: F-05, F-07, F-08, F-11.
- KISMİ: F-04, F-09 (bilinçli karar kaydı var ama gerekçesi tutmuyor), F-10, F-13 (belgelenmiş sözleşme),
  F-06, F-12 (erişim yolu daraltıldı).
- Doğrulayıcının düzelttiği iddialarım: F-06'daki "kapatacak hiç uç nokta yok" ifadesi yanlıştı; F-07'nin önemi
  Orta değil Yüksek.
- Harness gerçek `DualScreenApplication.Build` kullanıyor (`ProbeHarness.cs:58`); üretimden sapan yalnız iki
  kısayol var: P03'ün SQL ile iptal etmesi (gerçek recall yoluyla aynı son durum) ve P06'nın servisi doğrudan
  çağırması (HTTP uç noktası gövdeyi olduğu gibi iletiyor).

## 8. Kapsam matrisi

Hücre değerleri: `Pnn` geçen probe (sağlam) · `F-nn` bulgu · `Mnn` mutasyon · `K` yalnız kod okuması ·
`—` denetlenmedi.

| Adım \ Boyut | Mutlu yol | Tekrar/idempotency | Eşzamanlılık | Kardinalite | Yanlış durum | Yetki/sahiplik | Hata yolu/atomiklik |
| --- | --- | --- | --- | --- | --- | --- | --- |
| İndirim | P10 | K (V1-RMD-112 anahtar kontrolü) | F-05 (P12) | P15 | F-05 | — | K |
| Bahşiş | P10 | K | — | P15 | K | — | K |
| Bölme tasarımı | — | — | — | — | F-11 | — | — |
| Nakit tahsilat | P10 | P11, F-12 | P11 | P15 | F-04, F-07 | F-08 | M02, M08 |
| EFT tahsilat | P10 | K | — | P15 | M01 | — | — |
| Kart + elle onay | P16 | K | — | — | P07, M09 | M10 | P07 |
| Dağıtım tavanı | P10 | — | K (danışma kilidi) | P15 | F-05 | — | M05 |
| Hesap kapanışı | P10 | — | K (3 deneme) | P15 | F-06, M03, M07 (test yok) | — | — |
| Kasa oturumu | P10 | — | — | — | F-15 (K) | F-08, F-09 | F-14 (K), M04 (erişilemez) |
| Gün sonu | — | — | — | — | — | — | F-10 |
| Mutabakat raporu | P16 | — | — | — | — | — | — |

## 9. Denetlenemeyenler ve sınırlar

- Gerçek kart terminali, mali belge, yemek kartı, QNB: dış erişim yok. Banka kartı bugün her zaman
  `RequiresReconciliation` dönüyor (yer tutucu handler); elle onay akışı denetlendi.
- İade akışı: V13-ALC-004 olarak hâlâ açık (V1-RMD-328); denetlenmedi.
- İstemci (tarayıcı) davranışı yalnız kod okumasıyla değerlendirildi (F-11, F-12); tarayıcı E2E koşulmadı.
- Mutasyon denetiminde her mutasyon yalnız korumanın sahibi olan modül testleri ve aynı akışın Host HTTP
  testleriyle koşuldu; tüm çözümle değil.
- Denetim ortamı: .NET SDK 10.0.302 + .NET 8.0.30 runtime (Docker), PostgreSQL 18.6, `psql`/`pg_dump` 16.15.

## 10. Önerilen düzeltme görevleri (bu görevde yapılmadı)

Her biri ayrı bir görev olarak açılmalı; öncelik sırasıyla:

1. F-01/F-02: CI'ı yeniden çalışır hale getirmek (Actions limit/fatura durumu + lock dosyalarını yeniden üretmek) —
   diğer tüm düzeltmelerin ön koşulu.
2. F-03: üç kırmızı paketi düzeltmek; M07 ve kardinalite (P15) için kalıcı testler eklemek.
3. F-04: nakit tahsilatta çözülmemiş-ödeme kilidi (EFT ile aynı `bill-settlement` kilidi + kontrol).
4. F-05: indirimde "yeni ödenecek ≥ dağıtılmış" kontrolü, tahsilatla aynı kilit altında.
5. F-06: "kart çekilmedi" çözümünden sonra kapanışı yeniden denemek.
6. F-07, F-08, F-09: nakit tahsilatta hesap durumu ve oturum-terminal kontrolü; mutabakat yetkisi.
7. F-10, F-11: gün sonu cirosunu ödemelerden türetmek; bölme DTO'sunda düzeltilmiş tutar.
8. F-13: yer tutucu politikaların gerçek kurallarla değiştirilmesi veya ilgili yüzeyin kapalı tutulması.
