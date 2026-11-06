# V1-RMD-844 sonuç

Kapatılan (F0897, ikinci yarı): ödeme alma (tenders grubundaki tüm uçlar), ekranda gösterme, makbuz, cari hesaba yazma ve nakit tahsilat uçları artık V1-RMD-843'teki hesap-terminal kuralına bağlı. Başka kasanın hesabı `BILL_OTHER_TERMINAL` (403) alır; `cash.session.override` sahibi geçer.

Sahiplenme sırası (inceleme bulgusuyla düzeltildi): sahip denetimi uçtan önce, sahiplenme uç başarıyla döndükten sonra yapılır. Yetkisiz, geçersiz gövdeli veya reddedilen yazma hesabı kilitlemez. İki kasanın aynı anda sahipsiz hesaba başarılı yazması çok nadir bir yarıştır; ikinci sahiplenme kaybeder, işlem geri alınmaz (hesap zaten ödenmiş sayılır).

Eski testlerde iki güncelleme: yönetici tohumuna `cash.session.override` eklendi (yönetici hesabı her kasadan işler), sayfalama testi hesabı sahibi kasadan ödüyor.

## Doğrulama
- Kırmızı `red.log`: 10 yeni test, kural kapalıyken düşüyor.
- Yeşil: PaymentTender 92/92 (`green-PaymentTender.log`, yeni sınıf 11 test), CashSession 106/106, Billing 233/233 (`green-Billing.log`, filtre değişikliği sonrası). CustomerAccounts 22/22 filtre değişikliğinden önce koşuldu; account-charge ucu yeni sınıfta ve PaymentTender'da sınanıyor.
- Mutasyon (`mutations.log`, n1-n6, hepsi `cmp` ile geri alındı): tenders grubu, show-on-display, makbuz, cari, nakit uç filtresi kaldırılınca ve sahiplenme yetkiden önceye alınınca (n6) testler düşüyor.

## Koordinatöre not
- Kart onay/ret uçlarında (confirmations approve/reject) onaylayan yönetici override sahibi değilse ve hesap başka kasadaysa engellenir; bilinçli kabul edildi (varsayılan yönetici rolü override taşıyor).

## İnceleme
- qa-guvenlik-yetki: sahiplenme yetki ve doğrulamadan önceydi (Orta) -> sahip denetimi uçtan önce, sahiplenme başarıdan sonra; test + mutasyon n6. Eski testteki yönetici tohumu notu kabul edildi (yönetici yolu yeni sınıfta sınanıyor).
- qa-backend: iptal belirteci (sahiplenme `CancellationToken.None`), iade tamamlamada aynı sıra (düzeltildi, Billing 233/233 `green-final-billing.log`); yarış ve reddedilen kart ile sahiplenme kabul edildi.
- Doğrulama turu (qa-bulgu-dogrulayici): yarışta para riski yok (tüm ödeme türleri `bill-settlement:{billId}` kilidi ve kalan tutar sınırı altında); tek düzeltilmesi gereken iade tamamlama idi, yapıldı.
- Not: ekranda gösterme (POST) da başarıda hesabı sahipliyor; tasarım kararı olarak bırakıldı.
