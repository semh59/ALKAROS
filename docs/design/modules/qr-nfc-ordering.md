# Müşteri Self-Servis Sipariş (QR + NFC) — Tasarım Kararları

> **Durum:** Konuşularak kilitlendi (Semih, 2026-09-07). Faz 0'ın (`docs/design/foundations.md`)
> modül sıralamasında "Müşteri (QR)" olarak listelenen modülün erken çekilmiş
> hâli — hem backend hem frontend kapsıyor.
> **Bağlam:** Bugün `src/Modules` altında bu işlevin **hiçbir kodu yok**
> (yalnızca kullanılmayan `OrderSource.Qr` enum değeri ve genel amaçlı
> `ConfirmationStatus`/`PendingConfirmation` durum makinesi var — ikisi de
> sıfırdan inşa edilecek).

## 1. İki giriş kanalı, iki güven seviyesi

| Kanal | Fiziksel varlık kanıtı | Mutfağa gitme koşulu |
| --- | --- | --- |
| **NFC** (dokunma) | Güçlü — fotoğrafla/uzaktan çalınamaz | Onaysız, doğrudan |
| **QR** (kamera ile tarama) | Zayıf — fotoğraflanıp paylaşılabilir (gerçek, belgelenmiş bir dolandırıcılık deseni: bir restoranda tekrar kullanılan bir QR ile zamanla 60.000$'lık sahte sipariş birikmiş) | **Her zaman garson sözlü onayı + sistemde onay** |

İkisi de aynı sipariş sayfasına/aynı koda çıkar — fark yalnızca hangi kanaldan
geldiklerinde ve o kanalın güven seviyesine göre uygulanan kuralda.

## 2. NFC akışı

- Masaya bir NFC etiketi (NTAG213 sınıfı, ucuz, toplu) yapıştırılır; içinde
  yalnızca o masanın sabit linki vardır.
- Telefonla dokunma → işletim sistemi seviyesinde otomatik tarayıcı açılışı
  (Android ve iOS 11+'ta yerleşik, ek uygulama gerekmez — QR'ı kamerayla
  taramakla aynı sürtünme seviyesi).
- Masa **Available** ise, NFC ile gelen ilk sipariş **kendiliğinden masayı
  Occupied yapar** (self-check-in) — garson beklemeden.
- Etiketin URL'sini yazmak (tek seferlik, masa başına) ücretsiz hazır
  uygulamalarla (NFC Tools, NXP TagWriter) yapılır, özel bir yazılım/kod
  gerekmez.

## 3. QR akışı ve oturum modeli

> **Düzeltme (2026-09-07):** Bu bölüm önce "QR siparişi masa durumuna hiç
> dokunmaz" olarak yazılmıştı. Bu yanlıştı — önceden onaylanmış
> `docs/domain/table-reservation-policy.md` (`V0-DOM-005`, Semih onaylı,
> 2026-08-03) kararı zaten şunu tanımlıyor: `PendingConfirmation` masayı
> `Available → Reserved` yapar (fiziksel olarak dolu değil ama başkasına da
> verilmez — tam olarak "eski/mükerrer QR bir masayı süresiz kilitlemesin"
> sorununu çözen mekanizma budur). Aşağıdaki metin bu mevcut kararla
> uyumlu hâle getirildi.

- Fiziksel QR **sabit** kalır (`.../q/{tableId}` gibi, kendisi bir yetki
  değil, yalnız "hangi masa" bilgisi taşır).
- QR'dan gelen bir sipariş, masayı **hemen `Reserved`** yapar (`Available`
  ise) — "dolu" değil ama "başka bir misafire verilebilir" de değil; stok/
  porsiyon rezervasyonu **henüz yapılmaz** (yalnız onay anında, bkz.
  `V12-QRO-003`). Sipariş `PendingConfirmation` durumunda garsonun onay
  kuyruğuna düşer.
- Garson müşteriyle **sözlü teyit** eder (kim, ne, kaç kişi) ve sistemde
  onaylar → bu anda: masa `Reserved → Occupied` olur, sipariş `Accepted`'a
  geçer, porsiyon rezervasyonu ve mutfak bileti gerçekten oluşur.
- Ret/iptal/zaman aşımında masa `Reserved → Available` döner.
- **Otomatik zaman aşımı**: onaylanmamış bir QR siparişi, işletme ayarındaki
  süre (bkz. §5) dolunca kendiliğinden `Cancelled` olur ve masa
  `Reserved → Available` döner (stok/porsiyon rezervasyonu zaten hiç
  yapılmadığı için o tarafta bir geri alma gerekmez).
- Teknik not: bu periyodik temizlik, projede zaten var olan arka plan işi
  deseniyle (`CatalogPriceRecomputeHostedService` örneği) aynı kalıpta
  kurulacak, sıfırdan bir mekanizma değil.

## 4. Alkol / yaş kısıtlı ürün istisnası

Sektör bunu hâlâ tam çözmüş değil (Square/Toast gibi büyük oyuncularda bile
QR'dan alkolde personel elle kimlik kontrolü yapmak zorunda kalıyor). Bizim
kuralımız: **kanaldan bağımsız olarak** (NFC dahil), sepette yaş kısıtlı bir
ürün varsa sipariş her zaman garson onayına düşer — "Bu ürün için lütfen
garsonu çağırın" mesajıyla. Bu durumda NFC siparişi de QR'ınkiyle aynı yolu
izler: masa `Reserved` olur (`Occupied` değil), sipariş `PendingConfirmation`
kuyruğuna düşer — NFC'nin normal "doğrudan kabul" kısayolu bu tek istisnada
devre dışı kalır.

## 5. Onay zaman aşımı — ayarlanabilir, sabit kod değil

- Varsayılan: **5 dakika** (15 dakika çok uzun bulundu — hem garsonun normal
  servis ritminde bu sürede zaten masaya uğraması beklenir, hem de şüpheli
  bir siparişin sistemde uzun süre "canlı" kalmasını istemiyoruz).
- **İşletme ayarından değiştirilebilir** olmalı — projede zaten
  `kitchen.live_sync_enabled` gibi işletme bazlı ayarlar var (`Settings`
  modülü), aynı desen kullanılacak.
- **Not (Yönetim/Arka ofis modülü tasarlanırken hatırlanacak):** bu ayarın
  ekranı muhtemelen Yönetim/Arka ofis modülünün bir **Ayarlar** sekmesinde
  yaşayacak — modül sıramızda en sonda olduğu için, oraya geldiğimizde bu
  notu tekrar okuyup ekranı orada tasarlayacağız. Şimdiden bir yer/karar
  değil, yalnızca unutulmasın diye kayıt.

## 6. Kapsam dışı / kabul edilen kalıntı riskler

- **Fiziksel sticker sahteciliği** (birinin gerçek QR/NFC etiketinin üzerine
  sahte bir etiket yapıştırıp müşteriyi taklit bir siteye yönlendirmesi —
  ABD'de FBI bu konuda halka açık uyarı yayınlamış) — bu bizim yazılımımızın
  çözebileceği bir şey değil, işletmenin fiziksel önlemi (kolay sökülemeyen
  montaj, sayfada restoran adının belirgin gösterilmesi). Kayda geçirildi,
  çözülmedi.
- **Aktif bir QR/NFC oturumu sırasında linkin gerçek zamanlı paylaşılması**
  (masadaki biri linki anlık olarak dışarıdaki birine gönderirse) —
  yazılımla kanıtlanamayan bir fiziksel-varlık sınırı; zaten aynı masaya
  birden fazla kişinin bağımsız sipariş vermesini kasıtlı olarak
  desteklediğimiz için, bu durumun normal çoklu-misafir kullanımından
  ayırt edilmesi mümkün değil. Düşük değerli, kabul edilen bir kalıntı risk.

## Sonraki adım

QR/NFC modülünün tasarım kararları tamamlandı. Bir sonraki adım: Garson
modülüne (sipariş alma ekranı) dönüp devam etmek, ya da doğrudan bu modülü
görev(ler) hâline dökmek — Semih'in tercihine bağlı.
