# V13-GOV-007 - QNB e-Fatura adapter, unverified draft

Bu klasör, `V13-GOV-007`'nin (bkz. `plan/v1.4/qnb-esolutions/
V13-GOV-007-qnb-efatura-unverified-draft-authorization.md`) kendi Owned
surface'ı altında ürettiği tek deliverable'dır: `V14-QNB-001` (kayıtlı
kullanıcı sorgusu) ve `V14-QNB-002` (giden fatura gönderimi) gerçekten
başlayabilmeden önce, sadece kamuya açık QNB dokümantasyonuna dayalı,
doğrulanmamış bir taslak SOAP adaptörü.

## Yön değişikliği notu

Bu iş başta `V13-FSC-005` (QNB e-Adisyon) için planlanmıştı. Araştırma
gösterdi ki e-Adisyon'un kamuya açık hiçbir teknik dokümanı yok, ve zaten
seçilen Token/Beko cihazı kendi başına bir "YN ÖKC/e-Adisyon cihazı" —
yani `GATE-V13-FSC-STRATEGY`'nin Token/Beko dalını seçmesi çok daha
olası. Semih onayıyla yön, gerçekten ihtiyaç duyulacak ve iyi dokümante
olan **e-Fatura** entegrasyonuna çevrildi.

## Neden burada, `src/Modules/Invoicing/Qnb/**` altında değil

`V0-QNB-001` hâlâ `Blocked` (gerçek test tenant credential'ı yok).
`plan/TASK_STANDARD.md:57`/`:97` gereği bu bağımlılık `Done` olmadan
`V14-QNB-001/002`'ye `InProgress` verilemez. Üstelik bu ikisi ayrıca
`V14-CST-001`/`V14-INV-002/003` gibi henüz hiç yazılmamış v1.4 domain
nesnelerine de bağımlı. Bu taslak, o kuralları bypass etmeden ilerleyebilmek
için **`ALKAROS.slnx`'e hiç eklenmeyen, bağımsız derlenip test edilen**
bir proje olarak burada tutuluyor.

## Doğrulanan vs DOĞRULANMAYAN

**Doğrulanan (gerçek QNB dokümantasyonuna karşı, 23/23 test yeşil):**
- SOAP zarf şekli: `wsLogin`/`logout` (userService), `belgeGonderExt`/
  `gidenBelgeDurumSorgulaExt`/`kayitliKullaniciListeleExtended`
  (connectorService).
- `belgeGonderExt`'in **MD5 belge hash'i** doğru hesaplandığı ve doğru
  alanla (`belgeHash`) gönderildiği.
- `gidenBelgeDurumSorgulaExt`'in QNB'nin kendi kayıtlı GERÇEK örnek
  response'unu (durum=3, gonderimDurumu=-1, ettn dahil) doğru parse
  ettiği.
- `kayitliKullaniciListeleExtended`'in base64 ZIP response'unu doğru
  çözdüğü.
- **Tam durum kodu ağacının** (`durumKodu` 1/2/3 → `gonderimDurumu`
  -2..4 → `yanitDurumu` -1..2) `QnbDocumentStatusInterpreter` ile doğru
  modellendiği — QNB'nin kendi örnek kodunun İÇİNDEKİ çalışan if/else
  mantığından çıkarıldı, tahmin değil.
- SOAP fault'un HTTP 500 ile geldiğinde bile doğru okunduğu (**bu turda
  yakalanan gerçek bug**: ilk yazımda kod önce HTTP status kontrolü
  yapıp genel bir hata fırlatıyordu, asıl `faultstring`'i hiç okumadan —
  testler bunu yakaladı, sıra değiştirilip düzeltildi).

**DOĞRULANMAYAN (gerçek test tenant olmadan doğrulanamaz):**
- Gerçek bir QNB sunucusuna hiç bağlanılmadı.
- `gonderimDurumu == 3` durumunun 6+ farklı senaryosunun (aynı sayısal
  kod altında, yalnız serbest metin `gonderimCevabiDetayi` ile ayrışan)
  gerçek metinleri hiç görülmedi — `QnbDocumentStatusInterpreter` bu
  durumu bilerek `RequiresHumanReview` olarak işaretliyor, tahmin
  yürütmüyor.
- Hangi çağrılarda WS-Security `UsernameToken` header'ının da (cookie'ye
  ek olarak) zorunlu olduğu.
- Gerçek rate limit sayıları (yalnız "günde 1 defa yeterli" önerisi var).
- İptal/düzeltme (cancellation) — kamuya açık dokümanda hiçbir API
  metodu bulunamadı, bu taslak hiç kapsamıyor (`V14-QNB-005`'in kendi
  görevi).

## Nasıl çalıştırılır

```
cd evidence/V13-GOV-007/qnb-efatura-adapter-draft
dotnet test tests/QnbEFaturaAdapterDraft.Tests.csproj
```

## `V14-QNB-001/002` gerçekten başladığında yapılacaklar

1. `src/Modules/Invoicing/Qnb/RegisteredUser/` ve `Outgoing/` altında
   gerçek projeleri oluştur (`ALKAROS.Invoicing.csproj`'a dahil et).
2. `QnbSoapClient`'ı GERÇEK bir test tenant'a karşı çalıştır; her
   varsayımı (namespace URI'ları, WS-Security zorunluluğu, gerçek rate
   limit) doğrula veya düzelt.
3. `QnbDocumentStatusInterpreter`'ı `V14-QNB-004`'ün (invoice
   reconciliation) gerçek ihtiyacına göre uyarla — özellikle
   `gonderimDurumu==3` belirsizliğini gerçek `gonderimCevabiDetayi`
   metinleriyle netleştir.
4. `V14-QNB-001/002`'nin kendi Acceptance evidence'ını (gerçek sandbox
   transcript'i) üret.
