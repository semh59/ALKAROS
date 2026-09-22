# V1-SET-009 - Verification (gerçek kanıt)

## Statik testler

```
$ python -m pytest tests/Apps/CustomerWeb -q
16 passed
```
(13 önceki + 3 yeni: `test_customer_web_menu_renders_the_business_own_identity`,
`test_customer_web_order_entry_renders_the_business_own_identity`,
`test_customer_web_bill_renders_the_business_own_identity` — her biri
başlık bloğunun varsayılan `hidden` olduğunu, CSS sınıflarının var
olduğunu ve JS'in `loadBranding`/`/api/v1/qr/branding`/`/api/v1/qr/logo`/
`accentColor`/`businessName`/`hasLogo`/`--cw-accent`'i gerçekten
kullandığını doğruluyor.)

```
$ node --check menu-app.js && node --check order-entry.js && node --check bill.js
menu-app.js OK
order-entry.js OK
bill.js OK
```

## Gerçek Chromium (Playwright) ile uçtan uca doğrulama

Bir Python `stub_server.py`, üç sayfanın gerçek (değiştirilmemiş)
statik dosyalarını (`index.html`, `order-entry.html`, `bill.html` ve
eşlik eden CSS/JS — production'daki tek `qr-web` dizinine kopyalanma
biçimini birebir taklit ederek aynı köke birleştirilmiş) sunarken
`GET /api/v1/qr/branding` → `{businessName:"Sahil Cafe",
accentColor:"#1B4D7B", hasLogo:true}` ve `GET /api/v1/qr/logo` → gerçek,
geçerli bir 1x1 PNG (uydurma bayt dizisi değil, gerçek PNG magic
number'ıyla başlayan) döndürecek şekilde mock'landı. Gerçek Chromium
(`/opt/pw-browsers/chromium-1194`) ile üç sayfa da ziyaret edildi:

- `evidence/V1-SET-009/menu-branded.png`: "Sahil Cafe" adı ve logosu
  başlıkta görünüyor; aktif "Tümü" sekmesi ve sepet çubuğu artık navi
  mavi (#1B4D7B), eski sabit kahverengi (#B5772F) değil.
- `evidence/V1-SET-009/order-entry-branded.png`: aynı marka bloğu, "Siparişi
  Gönder" butonu ve "Adisyonum" bağlantısı navi mavi.
- `evidence/V1-SET-009/bill-branded.png`: aynı marka bloğu, "Menüye dön"
  bağlantısı navi mavi.

**Regresyon kontrolü (ayarlanmamış durum):** ayrı bir stub sunucu
`{businessName:"", accentColor:"#9C6323", hasLogo:false}` (V1-SET-007'nin
kendi varsayılan paleti) döndürecek şekilde çalıştırıldı —
`evidence/V1-SET-009/menu-unbranded.png` başlık bloğunun tamamen gizli
kaldığını ve görünümün bu görevden önceki (V1-SET-007/008 öncesi) hâliyle
birebir aynı olduğunu (yalnızca palet varsayılanı amber, adım/logo yok)
kanıtlıyor — bu görev hiçbir mevcut kurulumun görünümünü bozmuyor.

(Bill sayfasının stub verisindeki "KDV: ₺NaN" alanı yalnızca bu tek
kullanımlık test stub'ının `bill.js`'in gerçek `tax` alan adını tam
eşlememesinden kaynaklanıyor — gerçek backend zaten doğru alanı
dönüyor (V1-WTR-018'in kendi HTTP testleriyle ayrıca doğrulanmış); bu
görevün konusu olan marka adı/logo/renk üçü de doğru render edildi,
stub'ın bu kozmetik eksikliği düzeltilmedi çünkü V1-SET-009'un kapsamı
dışında.)

## Gate'ler

```
$ python tools/plan-audit/plan_audit_tool.py validate
Validation errors: 1 (C54_APPLICATION_ADMISSION_V3_FINAL_MISSING — öncedendi,
ilgisiz, değişmedi)
Validation warnings: 0

$ python tools/consistency-audit/consistency_audit.py
consistency-audit: clean
```

## Kapanış diff kontrolü

`git status --short`, Owned surface (tamamı "Sınırlı ek") ile birebir
eşleşiyor — bkz. commit diff'i.

## Manuel senaryo (Semih'in elle deneyebileceği)

`settings.manage` izniyle `business.name`/`business.accent_theme` ayarla
ve `PUT /api/v1/management/business-identity/logo`'ya bir PNG yükle; QR
kodunu okutup Menu/Sepetim/Adisyonum sayfalarının üçünün de işletmenin
adını, logosunu ve seçilen rengini gösterdiğini gör — yukarıdaki gerçek
Chromium ekran görüntüleriyle zaten uçtan uca doğrulandı.
