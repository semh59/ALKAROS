# V1-RMD-469 - KDV dahil satır fiyatı: kasa, müşteri ekranı ve adisyon sayfası

## Değişiklikler

- Kasa ürün kartı menü fiyatını olduğu gibi gösterir ("KDV dahil" etiketiyle); `grossUnitPrice` ve fiyatı yeniden şişiren hesap kaldırıldı.
- Toplam satırları artık toplanır gibi okunmaz: KDV satırı "İçindeki KDV" (kasa, müşteri ekranı, müşteri adisyon sayfası `bill.html`).

## Kanıt

- `typecheck.log`, `lint.log`, `vitest-all.log` (56 dosya, 419 test), `build.log`: hepsi exit 0.
- Yeni test `shows a product at its menu price because that price already includes KDV` ve güncellenen `CustomerDisplay` testi (560 TL, içindeki KDV 50,91).
- `mut/mutant-vitest.log`: eski `fiyat x (1 + oran)` hesabı ve eski "KDV" etiketi iki testi kırmızı yapar; dosyalar geri alındı, `cmp` özdeş.
- `real-host-trial.live.log`: gerçek Host, %10 KDV'li 100 TL ürün: ürün kartı `₺100,00 KDV dahil`; sipariş paneli `Ara toplam ₺100,00 İçindeki KDV ₺9,09 Toplam ₺100,00`.

## Açık kalan

- Garson PWA ve vanilla kasa istemcilerinde KDV satırı yoktur (kontrol edildi); mutfak/fiş çıktısındaki KDV gösterimi bu görevin dışındadır.
