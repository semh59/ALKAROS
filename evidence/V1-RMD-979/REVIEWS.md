# V1-RMD-979 incelemeleri

## 1. qa-backend
Bulgular: (Yuksek) kalem Active+NotSent oldugu icin duz void ucu stogu geri vermeden iptal edebilir; (Orta) hesap acma yarisi (siparis kilitlenmiyor); (Orta) anahtar zorunlu degil; (Orta) tekrar kapsami siparis icermiyor; (Dusuk) mesaj 0-999; (Dusuk) KDV profili yoksa 0 (projede yerlesik desen).
Aksiyon: kalem artik Served saklaniyor (yenmis urun; CanVoid/CanVoidSent false, sade iptal yollari kapali; test eklendi); IdempotencyKey zorunlu (400 IDEMPOTENCY_KEY_REQUIRED) ve tekrar kapsami siparisi icerir; mesaj duzeltildi. Acik: hesap acma ile eszamanli ekleme penceresi (BillingSplitStore siparisi kilitlemiyor) ayri gorev konusu.

## 2. qa-test-kalitesi
Bulgular: M3 esdeger degil (kod REASON_REQUIRED), miktar siniri, fiyatsiz urun, Draft, kapali siparis, bayat surum, void etkilesimi testleri yok.
Aksiyon: 12 test eklendi (toplam 20): REASON_REQUIRED kodu, 4 miktar degeri, fiyatsiz urun 404, Draft->Submitted, iptal edilmis siparis 409, anahtar zorunlu, ayni anahtar baska siparis, bayat surum 409, CanVoid false. Mutantlar M7-M9 eklendi ve oldu; M3 ve M10 savunma katmani nedeniyle yanit kodu ayni kaldigi icin yasiyor (M3 artik REASON_REQUIRED testiyle olu degil ise bu not: M3 testi kod kontrolu icerir).
Acik: yetersiz stokta geri alma testi ve terminal-siparis eslesmesi (mevcut ucların ortak davranisi) yapilmadi.
