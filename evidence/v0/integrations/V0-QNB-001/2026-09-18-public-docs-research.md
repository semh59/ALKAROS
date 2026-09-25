# V0-QNB-001 - QNB eSolutions kamuya açık API dokümantasyon taraması (2026-09-18)

- Task ID: V0-QNB-001
- Bu belge Status'u değiştirmez — Task hâlâ `Blocked`. Aşağıdaki bulgular
  yalnız `qnbesolutions.com.tr`'nin kamuya açık dokümantasyonundan derlenmiş;
  gerçek test tenant credential'ı, imzalı "Özel Entegrasyon" sözleşmesi
  veya gerçek document lifecycle transcript'i içermez.

## Kaynak

- `https://www.qnbesolutions.com.tr/api-docs-tr-final.html` — sayfa ilk
  bakışta boş bir kabuk gibi görünüyor (WebFetch bunu doğru okuyamadı),
  ama gerçek içerik bir JS `CODE_LIBRARY` objesi içinde gömülü
  (satır ~2591-8704, ham HTML'de). Bu objenin tamamı ham metin olarak
  bu klasöre kaydedildi: `qnb-esolutions-code-library-raw.txt` (6114
  satır) — SOAP XML + C# + Java örnekleri, Token'ın Postman koleksiyonuna
  denk bir kaynak.
- `https://www.qnbesolutions.com.tr/destek/api-teknik` — "Test Ortamı
  İstek Formu" burada; yalnız temel şirket bilgisi (e-posta, unvan,
  il/ilçe, VKN) istiyor, önceden sözleşme şartı yok.

## Bulunan CODE_LIBRARY bölümleri (tam liste)

`start_overview`, `start_urls`, `efatura_flow`, `efatura_send`,
`efatura_status`, `efatura_download`, `efatura_lookup`,
`efatura_kayitliKullaniciListe`, `efatura_listIncoming`,
`efatura_downloadIncoming`, `eirsaliye_flow`, `eirsaliye_send`,
`eirsaliye_status`, `eirsaliye_download`, `eirsaliye_lookup`,
`eirsaliye_kayitliKullaniciListe`, `eirsaliye_listIncoming`,
`eirsaliye_downloadIncoming`, `earsiv_send`.

Yani e-Fatura VE e-İrsaliye için gönderim/durum/indirme/sorgu/kayıtlı-
kullanıcı-listesi/gelen-kutusu akışlarının HEPSİ dokümante — V0-QNB-001'in
"In scope"unda istenen "outgoing/incoming e-belge, registered-user query,
idempotency, status query" maddelerinin tümüne karşılık geliyor.

## Gerçek test ortamı URL'leri (bulundu, tahmin değil)

| Servis | URL |
| --- | --- |
| e-Fatura connector (gönder/durum) | `https://erpefaturatest1.qnbesolutions.com.tr/efatura/ws/connectorService` |
| e-Fatura kullanıcı/login servisi | `https://erpefaturatest1.qnbesolutions.com.tr/efatura/ws/userService` |
| e-Arşiv web servisi | `https://earsivtest.qnbesolutions.com.tr/earsiv/ws/EarsivWebService` |
| e-Arşiv fatura görüntüleme | `http://earsivtest.qnbesolutions.com.tr/earsiv/goruntule.jsp` |

## Auth mekanizması

Token'ın REST/Bearer modelinden TAMAMEN FARKLI — **klasik SOAP + oturum
çerezi**:

```csharp
Login.userService login = new Login.userService();
login.CookieContainer = new System.Net.CookieContainer();
methods.CookieContainer = login.CookieContainer; // aynı cookie container paylaşılıyor
login.wsLogin(userId, password, "tr");
// ... işlemler ...
login.logout();
```

`wsLogin(userId, password, lang)` çağrısı bir SOAP servisi (`userService`)
üzerinden yapılıyor, dönen session cookie'si SONRAKİ TÜM servis
çağrılarında (`connectorService`) aynı `CookieContainer` ile taşınıyor.
WS-Security `UsernameToken` header'ı da bazı örneklerde ayrıca var (hangi
serviste zorunlu olduğu netleşmedi — gerçek credential'la doğrulanmalı).

## Belge gönderme (`belgeGonderExt`) — gerçek şema

```xml
<ser:belgeGonderExt>
  <parametreler>
    <vergiTcKimlikNo>3250566851</vergiTcKimlikNo>
    <belgeTuru>FATURA_UBL</belgeTuru>
    <belgeNo>KKT2022000000005</belgeNo>          <!-- yerel/kendi belge no'muz -->
    <veri>base64 UBL-XML</veri>
    <belgeHash>MD5 hash of veri</belgeHash>
    <mimeType>application_xml</mimeType>
    <belgeVersiyon>1.0</belgeVersiyon>
    <erpKodu/>
  </parametreler>
</ser:belgeGonderExt>
<!-- Response: string belgeGonderMsgOid (QNB'nin verdiği mesaj/işlem kimliği) -->
```

C# örneğinde belge hash'i **MD5** ile hesaplanıyor (`GetMD5Hash`) — belge
bütünlüğünü doğrulamak için gönderici tarafında hesaplanıp isteğe
ekleniyor.

## Durum sorgulama (`gidenBelgeDurumSorgulaExt`) — gerçek response şeması

```xml
<ser:gidenBelgeDurumSorgulaExt>
  <vergiTcKimlikNo>3250566851</vergiTcKimlikNo>
  <parametreler>
    <belgeNo>3ul4dvqhmd1095</belgeNo>
    <belgeNoTipi>OID</belgeNoTipi>      <!-- ya da yerel belge no tipi -->
    <belgeTuru>FATURA</belgeTuru>
    <donusTipiVersiyon>6.0</donusTipiVersiyon>
  </parametreler>
</ser:gidenBelgeDurumSorgulaExt>
```

Response alanları: `alimTarihi`, `belgeNo`, `durum` (örnek değer 3),
`ettn` (GİB'in verdiği UUID — e-Fatura Tekil Tanımlama Numarası),
`gonderimCevabiKodu`/`gonderimCevabiDetayi`, `gonderimDurumu` (örnek -1),
`yanitDurumu`/`yanitDetayi`, `ulastiMi` (bool), `yenidenGonderilebilirMi`
(bool — retry uygunluğu!), `yerelBelgeOid`.

**DÜZELTME (aynı gün, ikinci geçiş):** durum kodu tablosu aslında VAR —
ilk taramada atlanmış, C# örneğinin `GidenBelgeDurumSorgulaExt` metodunun
GÖVDESİNDEKİ if/else zincirinde (yorum değil, çalışan kontrol mantığı)
gömülü. Tam tablo:

| `durumKodu` | Anlamı |
| --- | --- |
| 1 | Alındı durumu — 2 veya 3 olana kadar beklenmeli |
| 2 | Fatura işleme hatası — düzeltip yeniden gönder |
| 3 | Fatura başarıyla işlendi — `gonderimDurumu`'na bak |

`durumKodu == 3` ise `gonderimDurumu`:

| `gonderimDurumu` | Anlamı |
| --- | --- |
| -2 | GİB'e gönderilemedi, iptal edildi, gönderilmeyecek |
| -1 | GİB'e gönderim kuyruğuna eklendi |
| 0 | GİB'e gönderilemedi, sistem yeniden deneyecek |
| 1 | GİB'e gönderilecek |
| 2 | GİB'e gönderilmiş |
| 3 | Gönderimde hata VEYA (aynı kod altında ayrı senaryolar) "GİB'le alıcı arasında" / "alıcıya iletilemedi, GİB 4 kez daha deneyecek" / "5 denemenin hepsi başarısız, aynı fatura no'suyla farklı zarfla yeniden gönderilebilir" / "alıcıya iletildi, sistem yanıtı bekleniyor" / "alıcıdan başarısız sistem yanıtı geldi, yeniden gönderilebilir" |
| 4 | Fatura alıcıya ulaştı — `yanitDurumu`'na bak |

`gonderimDurumu == 4` ise `yanitDurumu`:

| `yanitDurumu` | Anlamı |
| --- | --- |
| -1 | Temel fatura — karşıdan yanıt beklenmez (terminal, başarılı) |
| 0 | Ticari fatura — yanıt bekleniyor |
| 1 | Ticari fatura — RED uygulama yanıtı alındı |
| 2 | Ticari fatura — KABUL uygulama yanıtı alındı (terminal, başarılı) |

Bu, `V13-REC-001`/`V14-QNB-004` (mutabakat) için gereken "hangi durumlar
terminal/başarılı, hangileri retry-edilebilir, hangileri insan
müdahalesi ister" ayrımını net olarak kuruyor — örn. `gonderimDurumu==3`
altındaki alt senaryolardan biri "yeniden gönderilebilir" derken bir
diğeri "asla yeniden gönderme, alıcıyı uyar" diyor; ikisi aynı üst kod
altında ayrı `gonderimCevabiDetayi` metniyle ayrışıyor — tam ayrım metni
sabit değil, gerçek response'a bakmak gerekiyor.

**Hâlâ gerçekten eksik olanlar:**

1. Gerçek rate limit sayıları (sadece "günde 1 defa" gibi öneri var).
2. Hangi çağrılarda WS-Security header'ı da zorunlu (cookie yetiyor mu).
3. İptal/düzeltme (cancellation) için hiçbir API metodu bu taramada
   BULUNAMADI — `V14-QNB-005`'in kendi Acceptance evidence'ı zaten bunu
   öngörüyor ("bu kanıt yoksa task Blocked kalır").

## Kayıtlı kullanıcı sorgusu (`kayitliKullaniciListeleExtended`) — V0-QNB-001'in özellikle istediği madde

```csharp
byte[] kayitliMukellefListesi = methods.kayitliKullaniciListeleExtended(
    "EFATURA",              // ya da "EIRSALIYE"
    gecmisEklensin: 1,
    gecmisEklensinSpecified: true);
// Response: ZIP dosyası (byte[]) — kayıtlı mükellef listesini içeriyor
```

Alternatif overload'lar: `kayitliKullaniciListeleExtendedVknTckn(vknTckn,
urun)` (tek bir VKN/TCKN'nin kayıtlı olup olmadığını sorgular),
`kayitliKullaniciListeleExtendedTime(kayitZamani, urun, ...)` (belirli
tarihten sonraki kayıtlar). Yorum satırı önemli: **"Günde 1 defa çekmek
yeterlidir"** — rate-limit/best-practice notu, gerçek limitin sayısal
değeri yine dokümante değil.

## Hâlâ doğrulanamayan maddeler (Blocker'da açık kalanlar)

1. `durum`/`gonderimDurumu`/`yanitDurumu` alanlarının tam kod tablosu.
2. Gerçek rate limit sayıları (sadece "günde 1 defa" gibi öneri var,
   sert bir limit değeri yok).
3. `WS-Security UsernameToken` header'ının hangi servislerde zorunlu
   olduğu (`wsLogin` sonrası cookie yetiyor mu, yoksa her istekte de mi
   gerekiyor).
4. Timeout/retry sözleşmesi — `yenidenGonderilebilirMi` alanı var ama
   davranışın tam kuralı test tenant'a karşı denenmeden netleşmiyor.

## Sonuç

`V0-QNB-001` hâlâ `Blocked` — ama Token'da olduğu gibi, artık "hiçbir
teknik bilgi yok" değil, "teknik şema neredeyse tam, sadece gerçek test
tenant credential'ı ve birkaç kod tablosu eksik" durumunda. Somut sıradaki
adım: `qnbesolutions.com.tr/destek/api-teknik`'teki **Test Ortamı İstek
Formu**'nu doldurmak — bu, Token'ın aksine bir chatbot/telefon turu değil,
doğrudan bir form.
