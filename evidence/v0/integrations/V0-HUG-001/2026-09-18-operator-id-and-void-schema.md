# V0-HUG-001 - Yemek kartı operatorId listesi ve iptal (void) mekanizması (2026-09-18)

- Task ID: V0-HUG-001
- Bu belge Status'u değiştirmez — Task hâlâ `Blocked`. Bu,
  [[2026-09-18-postman-collection-schema]]'da tespit edilen iki açık
  sorudan ("yemek kartı operatorId eşlemesi yok", "cancel/refund
  endpoint'i yok") ilkini tamamen, ikincisini kısmen kapatan ek kamuya
  açık dokümantasyon taraması sonucudur.
- Kaynak: `developer.tokeninc.com` X Platform → Wire (`token-x-connect-wire/
  gelistirici-dokumani`) ve `x-platform/baslangic/banka-yemek-uygulamalari`
  sayfaları. Not: bu sayfalar **Wire (Kablolu)** entegrasyon dokümantasyonu
  altında; ALKAROS'un birincil hedefi **Cloud**'dur
  (`docs/domain/token-integration-path-decision.md`), ama `paymentItems`
  şeması (type/operatorId/status alanları) Cloud'un `Get Basket Details`
  response'unda da aynı şekilde göründüğü için (bkz.
  [[2026-09-18-postman-collection-schema]]) bu tablo Cloud'a da geçerli
  kabul ediliyor — **bu bir varsayım, gerçek cihaz/credential ile teyit
  edilmedi**.

## 1) Ödeme tipi (`paymentItems[].type`) tam tablosu — ÇÖZÜLDÜ

| Sabit | Değer | Anlamı |
|---|---|---|
| `PAYMENT_CASH` | 1 | Nakit |
| `PAYMENT_CHEQUE` | 2 | Çek |
| `PAYMENT_CREDITCARD` | 3 | Kredi Kartı |
| `PAYMENT_FOOD` | 7 | Yemek Kartı |
| `PAYMENT_ODEMESIZ` | 8 | Ödemesiz |
| `PAYMENT_IKRAM` | 9 | İkram |
| `PAYMENT_PUAN` | 11 | Puan |
| `PAYMENT_VPOS` | 12 | EPOS Ödeme |
| `PAYMENT_MOBILE` | 13 | Mobil Ödeme |
| `PAYMENT_EMONEY` | 14 | E-Para Ödeme |
| `PAYMENT_CHARITY` | 15 | Bağış |
| `PAYMENT_BOND` | 16 | Bond ile Ödeme |
| `PAYMENT_OPENACCOUNT` | 17 | Açık Hesap |
| `PAYMENT_MONEYTRANSFER` | 18 | Para Transferi |
| `PAYMENT_TRANSPORTATIONCARD` | 19 | Ulaşım Kartı |
| `PAYMENT_GIFTCARD` | 20 | Hediye Kartı |

## 2) Yemek kartı `operatorId` listesi — ÇÖZÜLDÜ (task'ın Blocker'ında sayılan 6 operatörün 6'sı da var)

Tümü `type: 7` ile birlikte gönderiliyor.

| Yemek Kartı | operatorId |
|---|---|
| App Temp (Test Uygulaması) | 1000 |
| TokenFlex | 1005 |
| Edenred | 1001 |
| Multinet | 1002 |
| Setcard | 1003 |
| Sodexo (muhtemelen Pluxee rebrand sonrası da aynı) | 1004 |
| Metropol | 1006 |

Örnek payload:
```json
"paymentItems": [
  { "amount": 1000, "type": 7, "operatorId": 1005 }
]
```

Bonus — kredi kartı banka `operatorId` tablosu da bulundu (Yapı Kredi 67,
Akbank 46, Garanti 62, Halkbank 12, İş Bankası 64, TEB 32, VakıfBank 15,
Ziraat 10, QNB 111, ING 133, vb. — bazı bankalarda değer boş/atanmamış:
alBaraka, DenizBank, ICBC, Ziraat Katılım).

**Not:** operatorId'nin dokümante olması, o operatörün ALKAROS'un
işletmesinde/terminalinde **fiilen aktif** olduğu anlamına gelmiyor —
`V0-HUG-001`'in kendi Blocker'ının ikinci maddesi ("her biri için ayrı
üye işyeri sözleşmesi") hâlâ geçerli. Bu sadece protokol/kod
seviyesindeki eşleme.

## 3) Cancel/Refund mekanizması — KISMEN ÇÖZÜLDÜ, yeni bir netlik var

Ayrı bir "refund" REST endpoint'i yok (bkz.
[[2026-09-18-postman-collection-schema]]). Bulunan gerçek mekanizma:

- **Fiş iptali `isVoid` alanı ile yapılıyor.** 300TR cihaz örneği: "fiş
  iptali için `sendPayment` isteği içinde `isVoid` değerini `true`
  göndermeniz yeterli." Cloud API'nin `Get Basket Details` response
  şemasında da basket seviyesinde `"isVoid": false` alanı zaten vardı —
  yani aynı mekanizma muhtemelen Cloud'da da geçerli.
- **Satış durumu `status` kodu `99` = "Fiş iptal"** (webhook/polling ile
  gözlemlenen sonuç kodu, önceki taramadaki `BASKET_COMPLETED` webhook
  kod tablosuyla — `0`/`-1`/`99` — tutarlı).

**Hâlâ açık soru:** Bu mekanizma "işlemi anında/aynı fiş içinde iptal et"
gibi görünüyor (void), klasik bankacılık anlamında "N gün sonra tamamlanmış
bir işlemi iade et" (refund/reversal) senaryosunu API'nin desteklediğine
dair hiçbir kanıt yok. `V13-HUG-003` ("refund-and-cancel") görevi
başladığında bu varsayımın gerçek bir test terminaliyle doğrulanması
gerekiyor — dokümantasyon bunun ötesine geçmiyor.

## Sonuç

`V0-HUG-001`'in Blocker'ında sayılan maddelerden **yemek kartı operatörü
eşlemesi artık tam olarak kapalı**. Kalan gerçek boşluklar:
1. Refund'un (tamamlanmış işlem sonrası iade) API üzerinden mümkün olup
   olmadığı — sadece void/isVoid mekanizması dokümante, gerçek refund akışı
   değil.
2. Gerçek client-id/client-secret, terminal erişimi ve her operatör için
   fiili üye işyeri aktivasyonu — hâlâ ticari bir adım, dokümantasyon
   araştırmasıyla çözülemez.

Task `Blocked` kalmaya devam ediyor (acceptance evidence hâlâ gerçek
cihaz/sandbox transkripti istiyor), ama artık kod tasarımı için gereken
şemanın neredeyse tamamı elimizde.
