# Licensing Contract — approved decision record

> **Task:** V0-LIC-001
> **Status:** Policy approved; task remains `Blocked` per `plan/GATES.md`'s
> `V0_DEFERRED_TASKS` (reopen stage `V20`, requires real license-server +
> real license-contract evidence — a policy decision alone does not close it)
> **Work type:** decision
> **Source basis:** PDF:II.2.24, PDF:III.26, PO:2026-09-18
> **Access date:** 2026-09-18
> **Approver:** Semih (ürün sahibi / named product owner ve legal approver) — 2026-09-18
> **Decision type:** Business decision (named approver)

## Selected decision

- **Activation unit:** tek işletme = tek lisans anahtarı. Anahtar, işletmenin
  kimliğini (`businessId`) ve izin verilen maksimum şube (fiziksel lokasyon)
  sayısını imzalı olarak taşır. Yeni şube açmak, lisansın şube kotasını
  yükseltmeyi gerektirir — kotanın üzerinde şube eklenmeye çalışılması mevcut
  çalışan şubeleri etkilemez, yalnız yeni şubenin aktivasyonunu reddeder.
- **Machine binding:** lisans yalnız `ALKAROS.Host`'un çalıştığı ana sunucu
  makinesine bağlanır (donanım parmak izi — stabil donanım kimliklerinin
  (anakart seri no, birincil disk seri no, birincil ağ arayüzü MAC'i) tuzlanmış
  hash'i). Kasa tableti, mutfak ekranı, garson PWA cihazı gibi istemciler
  lisans kotasından düşmez ve serbestçe değişebilir.
- **Offline grace period:** Host, lisans sunucusuna en son başarılı doğrulama
  zamanını yerel olarak saklar. Sunucuya erişilemediği her durumda işletme
  **14 gün** tam kapasiteyle (hiçbir özellik kısıtlanmadan) çalışmaya devam
  eder. 14. günün sonunda hâlâ başarılı bir doğrulama yapılamamışsa lisans
  `grace-expired` durumuna geçer (bkz. aşağıdaki "Geçersiz lisans davranışı").
- **Saat geri alma (clock rollback) koruması:** grace-period sayacı, sistem
  saatine değil, yalnız artan (monotonic, geriye sarılamayan) ve yerelde
  saklanan "en son gözlemlenen zaman" değerine göre ilerler. Sistem saati
  geriye alınırsa sayaç sıfırlanmaz/uzamaz — geri alınmış saat, grace
  süresini asla yapay olarak artıramaz. Ancak bu koruma **hiçbir zaman**
  restoranı durdurmaz; yalnız kötüye kullanımı (grace süresini saat oynayarak
  uzatmayı) önler.
- **Transfer/kurtarma:** kendi kendine hizmet. İşletme sahibi/yöneticisi
  ayarlar ekranından mevcut cihazı lisanstan çıkarır, ardından yeni sunucu
  donanımında aynı anahtarla aktive eder — destek ekibi onayı beklenmez.
  Kötüye kullanımı sınırlamak için 30 günlük kayan pencerede en fazla 2
  transfere izin verilir; bu sınırın aşılması yalnız yeni transferi reddeder,
  hâlihazırda aktif olan kurulumu etkilemez.
- **Geçersiz lisans davranışı (failure behavior):** lisanslama hizmetinin
  kaybı veya `grace-expired` durumu **hiçbir zaman** sipariş alma, mutfağa
  gönderme, ödeme alma veya kapama gibi restoran operasyonunu otomatik olarak
  durdurmaz. Tek gözlemlenebilir etki: yönetici/kasiyer oturumlarında kalıcı,
  kapatılamaz bir Türkçe uyarı şeridi ("Lisans doğrulanamadı, lütfen internet
  bağlantısını kontrol edin veya destek ile iletişime geçin") ve bir denetim
  (audit) kaydı. Otomatik bir kilitleme/durdurma davranışı bu kararla
  **reddedilmiştir** (bkz. Rejected alternatives) — yalnız ayrı, açıkça
  onaylanmış, manuel bir yönetici eylemiyle (ör. sözleşme feshi sonrası
  destek ekibinin elle devre dışı bırakması) tetiklenebilir; bu, `V0-LIC-001`
  kapsamı dışıdır (Out of scope: "uzaktan kapatma davranışı işletme
  tarafından onaylanmadı").

## Why

- **Şube başına değil işletme başına lisans:** ALKAROS'un hedef müşteri
  profili (tek şubeli/az şubeli bağımsız restoranlar, freelance entegrasyon
  modeli — bkz. `[[kasa-payments-token-audit-chain]]`'in iş modeli notu)
  için en düşük sürtünmeli model budur; büyük zincirler zaten şube kotası
  yükseltmesiyle desteklenir, ayrı lisans yönetimi yükü getirmez.
- **Sadece sunucuya bağlama:** `V0-ARC-002` (local-first sync contract),
  ALKAROS mimarisinin tek bir yerel `Host` etrafında kurulduğunu zaten
  tanımlıyor — istemci cihazlar (tablet, telefon) doğası gereği kırılgan/
  değişken donanımlardır (düşer, kırılır, değişir); bunları lisans kotasına
  bağlamak günlük operasyonu gereksiz kırılgan hale getirirdi.
- **14 günlük grace period:** bir restoranın internet/donanım arızasında
  günler içinde servisi durduramayacağı prensibiyle (`Acceptance evidence`:
  "ana restoranın faaliyetlerini beklenmedik bir şekilde durduramaz")
  tutarlı; aynı zamanda iptal edilmiş bir lisansın haftalar boyunca fark
  edilmeden çevrimdışı çalışmasını da engelleyecek kadar kısa.
- **Otomatik durdurma yok:** bu, `Out of scope`'ta zaten reddedilen "uzaktan
  kapatma" davranışıyla doğrudan tutarlı — bir yazılım/lisans hatası hiçbir
  zaman canlı bir restoranın operasyonunu (özellikle ödeme/mutfak akışını)
  kesmemelidir; bu risk, gelir kaybı ve gerçek müşteri güvenini doğrudan
  etkiler.

## Rejected alternatives

- **Şube başına ayrı lisans** — reddedildi: hedef müşteri profili için
  gereksiz idari yük; büyük zincirler zaten kota yükseltmesiyle
  desteklenebiliyor.
- **Her istemci cihazı lisans kotasına dahil etme** — reddedildi: tablet/
  telefon gibi sık değişen donanımları kotaya bağlamak günlük operasyonu
  kırılgan hale getirir; ana sunucu tekil ve stabil bir bağlama noktasıdır.
- **7 günlük grace period** — reddedildi: kısa internet kesintilerinde bile
  riski gereksiz artırır.
- **30 günlük grace period** — reddedildi: iptal edilmiş bir lisansın bir ay
  boyunca fark edilmeden çalışmasına izin verir.
- **Destek onayı gerektiren transfer** — reddedildi: bir donanım arızası
  anında restoranın destek yanıtını beklemek zorunda kalması, bu kararın
  ana ilkesiyle ("beklenmedik durdurma yok") çelişir.
- **Grace-period sonunda otomatik kilitleme/salt-okunur moda geçme** —
  reddedildi: bu, fiilen "beklenmedik durdurma" ile aynı sonucu üretir;
  yalnız görünür uyarı + denetim kaydı seçildi, işlevsel kısıtlama değil.

## Invariants for consumers

- Hiçbir `V1-FND-*`/`V1-SEC-*`/ödeme/mutfak akışı, lisans durumuna (`Valid`,
  `GraceExpired`, `Unreachable`) bakarak sipariş/ödeme/mutfak işlemini
  reddedemez veya geciktiremez.
- Lisans durumu yalnız (a) yönetici/kasiyer oturumunda görünen bir uyarı
  şeridi ve (b) bir audit kaydı üretir — başka hiçbir yan etkisi yoktur.
- Grace-period sayacı yalnız monotonic yerel zamana göre ilerler; sistem
  saatinin geriye alınması sayaç durumunu asla iyileştiremez.
- Transfer, eski cihazı **önce** pasifleştirmeden yeni cihazı aktive etmez
  (aynı anda iki aktif donanım imzası olamaz) — ama pasifleştirme + yeni
  aktivasyon işletme sahibinin kendi eylemidir, destek onayı beklemez.
- Şube kotası aşımı yalnız *yeni* şube aktivasyonunu reddeder; hâlihazırda
  aktif şubelerin hiçbirini etkilemez.

## Affected tasks

- Depends on: `V0-ARC-002` (local-first sync — Host'un tekil, stabil bağlama
  noktası olduğu varsayımı), `V0-CMP-003` (KVKK envanteri — donanım parmak
  izi kişisel veri sınıfına girmez, yalnız donanım tanımlayıcısıdır).
- Consumers: `V20-LIC-001` (lisans anahtarı üretimi/imzalama), `V20-LIC-002`
  (lisans kurtarma tatbikatı) bu kararın invariant'larına göre tasarlanmalı;
  ikisi de bu karar `Done` olmadan `InProgress` olamazdı
  (`plan/TASK_STANDARD.md:57`/`:97`).

## Acceptance evidence

- Decision record with source, approver, rationale, rejected alternatives
  and re-evaluation-free invariants: above.
- Lisanslama hizmetinin kaybı senaryosu açıkça ele alındı: otomatik durdurma
  yok, yalnız görünür uyarı + audit. Geçersiz lisans davranışı ve kurtarma
  (transfer) süreci yukarıda tam olarak tanımlandı — artık açık değil.
