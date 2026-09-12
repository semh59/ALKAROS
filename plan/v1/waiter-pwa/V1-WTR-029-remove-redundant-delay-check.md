# V1-WTR-029 - Gereksiz iş yükü: gecikme kontrolünün kaldırılması

- Task ID: V1-WTR-029
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

Semih'in talimatıyla ("Başka iş yükü artışı yaptığımız ne varsa bul",
2026-09-12): V1-WTR-017'nin "hafif gecikme kontrolü" özelliği, garson
modülündeki EN yaygın karşılaşılan gereksiz iş yükü olarak tespit edildi
ve kaldırıldı.

**Bulgu:** `checkDelayAndSend`, uygulamadaki **her iki** "Gönder"
butonunu (menü ekranı VE hesap ekranı) kesiyordu — sepette hazırlama
süresi tahmini ≥10 dakika farklı olan 2 ürün varsa (ör. salata 5dk +
biftek 18dk — son derece sıradan bir kombinasyon), gönderim duruyor ve
garson "Birlikte gönder" / "Ayrı gönder" seçimini ZORUNLU yapmak
durumunda kalıyordu. Eşik (10 dk) düşük olduğu için bu, gerçek
siparişlerin büyük bir kısmında (herhangi bir başlangıç+ana yemek
kombinasyonu) tetiklenebilecek kadar sık.

**Neden kaldırıldı, ayarlanmadı:** V1-WTR-025 (kurs yönetimi, bu
görevden SONRA gelen bir özellik) aynı sorunu ("yemekler eşit olmayan
zamanlarda masaya gelmesin") çoktan, daha bilinçli bir şekilde çözüyor —
garson isterse her kaleme bir kurs numarası atıyor, `FireRound`/
`FireCourse` mutfağa gönderimi kendisi kademeye ayırıyor. `checkDelayAndSend`
ise kurs bilgisine HİÇ bakmıyordu — kursunu doğru ayarlamış bir garson
bile bu pop-up'ı yiyordu. İki mekanizma aynı sorunu iki kez çözmeye
çalışıyordu; biri (eski, kurs-farkında olmayan, EN sık kullanılan
butonu kesen) gereksizdi.

## Owned surface

- Sınırlı ek:
  - src/Clients/WaiterPwa/wwwroot/waiter-app.js (WaiterPwa sahipliğinde)
    — `checkDelayAndSend`, `confirmDelayChoice`, `productPrepTimeMinutes`,
    `DELAY_WARNING_THRESHOLD_MINUTES` tamamen silindi; `sendDraft`
    parametresiz, tam sepeti gönderen tek bir yola sadeleştirildi
    (`items`/`isPartialSend` dallanması kalktı — artık hiçbir çağıran
    kısmi bir alt küme göndermiyor); her iki "Gönder" butonu artık
    doğrudan `sendDraft()`'a bağlı.
  - src/Clients/WaiterPwa/wwwroot/waiter-app.css (WaiterPwa sahipliğinde)
    — `.delay-warning-text`, `.delay-choice-actions` kuralları silindi
    (yalnız bu özelliğin kendi sheet body'sinde kullanılıyorlardı).

## Out of scope

- `catalog.products.prep_time_minutes` alanının kendisi (sunucu tarafı,
  domain modeli) — dokunulmadı. PosTerminal'in Katalog ekranı bu alanı
  hâlâ yönetici için düzenlenebilir tutuyor (`CatalogWorkspace.tsx`,
  `setPrepTime`) — gelecekte başka bir özellik (ör. mutfak ekranında
  tahmini süre gösterimi) bu veriyi kullanabilir, bu yüzden alan
  kaldırılmadı, yalnız WaiterPwa'nın onu OKUYUP bir gönderim engeli
  üretmesi kaldırıldı.
- V1-WTR-025'in kurs sisteminin kendisi — dokunulmadı, zaten doğru
  çalışıyor.
- Bu oturumda ayrıca incelenip iş yükü artışı OLMADIĞI doğrulanan
  akışlar (kod okunarak, değişiklik yapılmadı): kişi sayısı sorma
  (V1-WTR-015, opsiyonel/buton ile açılıyor, göndermeyi bloklamıyor),
  koltuk/kurs seçici (V1-WTR-022/025, yalnız modifiyerli üründe açılıyor),
  void/komp/yardım/masa devri ekranları (istisnai aksiyonlar, para/stok
  etkisi olduğu için onay haklı).

## Dependencies

- V1-WTR-017
- V1-WTR-025

## Acceptance evidence

- `node --check src/Clients/WaiterPwa/wwwroot/waiter-app.js` → sözdizimi
  hatası yok.
- `tests/E2E/WaiterPwa` (gerçek Chrome + gerçek Postgres + gerçek Host,
  `node node_modules/@playwright/test/cli.js test --reporter=list`):
  tam paket 17/18 — tek başarısız olan, V1-WTR-028'in kendi
  Out-of-scope'unda zaten belgelenen, bu görevle ilgisiz makine/CPU
  rekabeti flake'i (`05-load-and-timing.spec.js` izole koşulduğunda 2/2
  temiz, üstelik bu görev tam olarak bu spec'in kendi "Gönder" akışını
  kullanıyor — regresyon olmadığının doğrudan kanıtı). Kalan 16 test
  (login, tam sipariş akışı, garson aksiyonları, zamanlama) hepsi
  değişmeden yeşil — hiçbiri artık var olmayan gecikme sheet'ine
  dokunmuyordu zaten.
- `python tools/consistency-audit/consistency_audit.py` → bu görevin
  değiştirdiği dosyalarda 0 ihlal.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0
  uyarı.

## Handoff

- None
