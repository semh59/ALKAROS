# V1-RMD-392 - CustomerWeb Bill Tur 2 denetimi: geçici bir poll hatası kalıcı hale geliyordu

- Task ID: V1-RMD-392
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-27

## Goal

Modül-modül arayüz denetiminin Tur 2'si (P1/P2/P4/T7/T8 derin geçişi), Modül 17 (son modül):
CustomerWeb'in salt-okunur canlı adisyon ekranı (`bill.js`). Sayfanın kendi üst bilgisi açıkça
"Bu ekran canlı güncellenir, garsonu çağırmanıza gerek yok." diyor ve 5 saniyede bir `/api/v1/qr/bill`
poll ediyor — `startPolling()`'in kendi yorumu da "a guest's own network blip must not permanently
freeze this page on a stale total" diyerek geçici hataların kalıcı olmaması gerektiğini zaten kabul
ediyordu.

Ama kod bunu tutmuyordu: `showError()` `errorState`'i (role="alert") görünür yapıyordu, ama
HİÇBİR YER bunu bir daha gizlemiyordu — `renderBill()` bile yalnızca `loadingState`'i gizliyordu.
Sonuç: gerçek bir müşterinin ilk poll'u (tek seferlik bir WiFi kesintisi) başarısız olursa, sonraki
her poll doğru şekilde adisyonu güncelleyerek gösterse bile, kalıcı bir "Adisyon şu anda
yüklenemedi…" hata banner'ı EKRANDA SONSUZA DEK KALIYORDU — hem doğru veri hem korkutucu hata aynı
anda görünüyor, üstelik bu ekranın bütün amacı olan "garsonu çağırmanıza gerek yok" vaadi tam da bu
anda boşa çıkıyordu (P2, saha gerçekliği — restoran WiFi'sinde tek seferlik kesinti sıradan bir
olay).

## Owned surface

- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Apps/CustomerWeb/Bill/wwwroot/bill.js
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Apps/CustomerWeb/Bill/test_customer_web_bill.py
- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/v1/ui-audit/UI_AUDIT_PROGRESS.md
- `plan/v1/remediation/V1-RMD-392-customerweb-bill-round2-audit.md`

## In scope

1. **[P2, Orta-Yüksek — saha gerçekliği] Bir kez gösterilen hata banner'ı asla gizlenmiyordu.**
   `renderBill()` artık her başarılı render'da `errorState`'i de gizliyor — ilk poll başarısız
   olup ikincisi başarılı olursa, hata artık ekranda takılı kalmıyor. Ayrıca yeni bir
   `hasRenderedOnce` bayrağı eklendi: en az bir kez gerçek bir adisyon başarıyla gösterildikten
   sonra yaşanan bir hata artık HİÇ gösterilmiyor (zaten ekranda doğru/güncel bir toplam varken
   üzerine korkutucu bir hata bindirmek yerine, sessizce bir sonraki poll'u bekliyor) — tam olarak
   `startPolling()`'in kendi yorumunun zaten vaat ettiği davranış.

## Out of scope

- Yok.

## Dependencies

- None

## Acceptance evidence

- `python -m pytest tests/Apps/CustomerWeb/Bill/test_customer_web_bill.py -q`: 8/8 geçti (bu
  görevin yeni testi dahil) — bu dosyanın kendi test yakınsaması, diğer CustomerWeb sayfalarıyla
  aynı statik-metin doğrulama biçimini kullanıyor (gerçek bir JS test koşucusu bu vanilla
  uygulamalar için hiç kurulu değil, bkz. V1-RMD-375/391'in aynı ailedeki emsal testleri).
- Mutation-check: yalnızca `bill.js` `git stash` ile geri alındı — yeni test GERÇEKTEN kırmızı
  oldu (`AssertionError: assert 'hasRenderedOnce' in ...`). `git stash pop` ile geri yüklendi,
  dosyanın tam paketi tekrar 8/8 yeşile döndü.

## Handoff

- None
